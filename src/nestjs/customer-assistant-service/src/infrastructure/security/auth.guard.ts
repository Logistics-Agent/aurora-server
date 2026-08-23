import {
  Injectable,
  CanActivate,
  ExecutionContext,
  UnauthorizedException,
  ForbiddenException,
  Logger,
} from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { CurrentUser } from './current-user.interface';
import { ActorType } from '../../domain/enums/actor-type.enum';
import { AuthIdentityMismatchException } from '../../domain/errors/assistant.errors';

@Injectable()
export class AuthGuard implements CanActivate {
  private readonly logger = new Logger(AuthGuard.name);
  private readonly internalServiceSecret: string;
  private readonly trustedServiceIds = ['Staff.Bff', 'Admin.Bff', 'System.Bff', 'financial-service', 'internal-gateway'];
  private readonly isProduction: boolean;

  constructor(private readonly configService: ConfigService) {
    this.internalServiceSecret =
      this.configService.get<string>('INTERNAL_SERVICE_SECRET') ||
      this.configService.get<string>('INTERNAL_SECRET') ||
      'aurora-internal-bff-secret-2026';
    this.isProduction = (this.configService.get<string>('NODE_ENV') || 'development') === 'production';
  }

  canActivate(context: ExecutionContext): boolean {
    const request = context.switchToHttp().getRequest();
    const headers = request.headers;

    const authHeader = headers['authorization'] || headers['Authorization'];
    const serviceId = (headers['x-service-id'] || '').toString();
    const internalSecret = (headers['x-internal-secret'] || '').toString();

    let isTrustedInternalOrigin = false;

    // 1. Verify Internal BFF / Gateway Origin (PATCH 8)
    if (serviceId && this.trustedServiceIds.includes(serviceId)) {
      if (internalSecret === this.internalServiceSecret || !this.isProduction) {
        isTrustedInternalOrigin = true;
      } else {
        throw new ForbiddenException(`Untrusted internal service credentials for service: ${serviceId}`);
      }
    }

    let tenantId: string | undefined;
    let userId: string | undefined;
    let customerId: string | undefined;
    let actorType: ActorType = ActorType.CUSTOMER;
    let roles: string[] = [];

    // 2. Extract Identity from JWT if present
    if (authHeader && authHeader.toString().startsWith('Bearer ')) {
      const token = authHeader.toString().substring(7).trim();
      const jwtClaims = this.decodeJwtClaims(token);

      if (jwtClaims) {
        tenantId = jwtClaims.tenant_id || jwtClaims['custom:tenant_id'];
        userId = jwtClaims.sub || jwtClaims.user_id;
        customerId = jwtClaims.customer_id || jwtClaims['custom:customer_id'];

        const rawRole = (jwtClaims.role || jwtClaims['cognito:groups']?.[0] || 'CUSTOMER').toString().toUpperCase();
        actorType = this.mapActorType(rawRole);
        roles = [rawRole];
      }
    }

    // 3. Process Headers vs Origin Trust
    const headerTenant = (headers['x-tenant-id'] || headers['tenant-id'])?.toString().trim();
    const headerUser = (headers['x-user-id'] || headers['user-id'])?.toString().trim();
    const headerCustomer = (headers['x-customer-id'] || headers['customer-id'])?.toString().trim();
    const headerActor = (headers['x-actor-type'] || headers['actor-type'])?.toString().toUpperCase();

    if (isTrustedInternalOrigin) {
      // Internal BFF: headers are trusted and authoritative
      if (headerTenant) {
        if (tenantId && tenantId !== headerTenant) {
          this.logger.warn(`[Observability] assistant_identity_mismatch: JWT tenant '${tenantId}' does not match header tenant '${headerTenant}'`);
          throw new AuthIdentityMismatchException(`JWT tenant '${tenantId}' does not match header tenant '${headerTenant}'`);
        }
        tenantId = headerTenant;
      }
      if (headerUser) {
        if (userId && userId !== headerUser) {
          this.logger.warn(`[Observability] assistant_identity_mismatch: JWT user '${userId}' does not match header user '${headerUser}'`);
          throw new AuthIdentityMismatchException(`JWT user '${userId}' does not match header user '${headerUser}'`);
        }
        userId = headerUser;
      }
      if (headerCustomer) customerId = headerCustomer;
      if (headerActor) actorType = this.mapActorType(headerActor);
    } else {
      // External / Public origin:
      if (headerActor && headerActor !== actorType.toString()) {
        this.logger.warn(`[Observability] assistant_untrusted_identity_header: Spoofed x-actor-type '${headerActor}' rejected for caller '${userId}'`);
        throw new ForbiddenException('External client cannot override actor-type via request headers.');
      }
      if (headerTenant && tenantId && headerTenant !== tenantId) {
        this.logger.warn(`[Observability] assistant_untrusted_identity_header: Spoofed x-tenant-id '${headerTenant}' rejected for caller '${userId}'`);
        throw new ForbiddenException('External client cannot override tenant-id via request headers.');
      }

      if (!tenantId || !userId) {
        if (!this.isProduction) {
          // Dev fallback when offline
          tenantId = headerTenant || 'a0000000-0000-0000-0000-000000000001';
          userId = headerUser || '11111111-1111-1111-1111-111111111111';
          customerId = headerCustomer || 'CUST-001';
          actorType = headerActor ? this.mapActorType(headerActor) : ActorType.CUSTOMER;
        } else {
          throw new UnauthorizedException('Valid authorization token or trusted service identity is required.');
        }
      }
    }

    if (!tenantId || !userId) {
      throw new UnauthorizedException('Missing required authentication context (tenant-id, user-id).');
    }

    const traceId = (headers['x-trace-id'] || headers['traceparent'] || '').toString();

    const currentUser: CurrentUser = {
      tenantId,
      userId,
      customerId,
      actorType,
      roles: roles.length > 0 ? roles : [actorType.toString()],
      permissions: [],
      traceId,
    };

    request.user = currentUser;
    return true;
  }

  private mapActorType(raw: string): ActorType {
    if (raw === 'STAFF') return ActorType.STAFF;
    if (raw === 'ADMIN') return ActorType.ADMIN;
    if (raw === 'SYSTEM') return ActorType.SYSTEM;
    return ActorType.CUSTOMER;
  }

  private decodeJwtClaims(token: string): Record<string, any> | null {
    try {
      const parts = token.split('.');
      if (parts.length < 2) return null;
      const payload = Buffer.from(parts[1], 'base64').toString('utf-8');
      return JSON.parse(payload);
    } catch {
      return null;
    }
  }
}
