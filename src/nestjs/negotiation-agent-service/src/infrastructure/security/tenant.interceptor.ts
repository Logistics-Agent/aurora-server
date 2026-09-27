import { Injectable, NestInterceptor, ExecutionContext, CallHandler } from '@nestjs/common';
import { RpcException } from '@nestjs/microservices';
import { Metadata, status } from '@grpc/grpc-js';
import { Observable } from 'rxjs';
import { ConfigService } from '@nestjs/config';
import { timingSafeEqual } from 'crypto';

@Injectable()
export class TenantInterceptor implements NestInterceptor {
  constructor(private readonly config: ConfigService) {}

  intercept(context: ExecutionContext, next: CallHandler): Observable<any> {
    const rpc = context.switchToRpc();
    const metadata = rpc.getContext<Metadata>();
    const tenantId = metadata?.get('x-tenant-id')?.[0]?.toString().trim();
    if (!tenantId) {
      throw new RpcException({ code: status.UNAUTHENTICATED, message: 'x-tenant-id metadata is required' });
    }
    if (this.config.get<string>('NODE_ENV') === 'production') {
      const configuredSecret = this.config.get<string>('INTERNAL_SERVICE_SECRET');
      const providedSecret = metadata?.get('x-internal-secret')?.[0]?.toString() || '';
      if (!configuredSecret) {
        throw new RpcException({ code: status.UNAVAILABLE, message: 'Negotiation service credentials are not configured' });
      }
      const expected = Buffer.from(configuredSecret);
      const provided = Buffer.from(providedSecret);
      if (expected.length !== provided.length || !timingSafeEqual(expected, provided)) {
        throw new RpcException({ code: status.UNAUTHENTICATED, message: 'Trusted service credentials are required' });
      }
    }
    rpc.getData().tenantId = tenantId;
    rpc.getData().userId = metadata?.get('x-user-id')?.[0]?.toString() || undefined;
    return next.handle();
  }
}
