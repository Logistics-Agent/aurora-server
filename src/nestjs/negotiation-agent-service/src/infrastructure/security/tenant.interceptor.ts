import { Injectable, NestInterceptor, ExecutionContext, CallHandler } from '@nestjs/common';
import { RpcException } from '@nestjs/microservices';
import { Metadata, status } from '@grpc/grpc-js';
import { Observable } from 'rxjs';

@Injectable()
export class TenantInterceptor implements NestInterceptor {
  intercept(context: ExecutionContext, next: CallHandler): Observable<any> {
    const rpc = context.switchToRpc();
    const metadata = rpc.getContext<Metadata>();
    const tenantId = metadata?.get('x-tenant-id')?.[0]?.toString().trim();
    if (!tenantId) {
      throw new RpcException({ code: status.UNAUTHENTICATED, message: 'x-tenant-id metadata is required' });
    }
    rpc.getData().tenantId = tenantId;
    return next.handle();
  }
}
