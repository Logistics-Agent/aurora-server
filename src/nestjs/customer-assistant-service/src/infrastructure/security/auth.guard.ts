import {
  Injectable,
  CanActivate,
  ExecutionContext,
  UnauthorizedException,
  ForbiddenException,
} from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { CurrentUser } from './current-user.interface';
import { ActorType } from '../../domain/enums/actor-type.enum';

@Injectable()
export class AuthGuard implements CanActivate {
  private readonly internalServiceSecret: string;
  private readonly trustedServiceIds = ['Staff.Bff', 'Admin.Bff', 'System.Bff', 'financial-service', 'internal-gateway'];

  constructor(private readonly configService: ConfigService) {
    this.internalServiceSecret =
      this.configService.get<string>('INTERNAL_SERVICE_SECRET') ||
      this.configService.get<string>('INTERNAL_SECRET') ||
      '';
  }

  canActivate(context: ExecutionContext): boolean {
    const request = context.switchToHttp().getRequest();
    const headers = request.headers;

    const serviceId = (headers['x-service-id'] || '').toString();
    const internalSecret = (headers['x-internal-secret'] || '').toString();

    let isTrustedInternalOrigin = false;

    // 1. Verify Internal BFF / Gateway Origin (PATCH 8)
    if (serviceId && this.trustedServiceIds.includes(serviceId)) {
      if (this.internalServiceSecret && internalSecret === this.internalServiceSecret) {
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

    // The BFF validates the user's session. Never decode an unverified JWT here.
    const headerTenant = (headers['x-tenant-id'] || headers['tenant-id'])?.toString().trim();
    const headerUser = (headers['x-user-id'] || headers['user-id'])?.toString().trim();
    const headerCustomer = (headers['x-customer-id'] || headers['customer-id'])?.toString().trim();
    const headerActor = (headers['x-actor-type'] || headers['actor-type'])?.toString().toUpperCase();

    if (isTrustedInternalOrigin) {
      // Internal BFF: authenticated headers are authoritative.
      if (headerTenant) {
        tenantId = headerTenant;
      }
      if (headerUser) {
        userId = headerUser;
      }
      if (headerCustomer) customerId = headerCustomer;
      if (headerActor) actorType = this.mapActorType(headerActor);
    } else {
      throw new UnauthorizedException('Trusted service identity is required.');
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

}
