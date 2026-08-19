import {
  Injectable,
  NestInterceptor,
  ExecutionContext,
  CallHandler,
} from '@nestjs/common';
import { Observable } from 'rxjs';
import { Metadata } from '@grpc/grpc-js';

@Injectable()
export class TenantInterceptor implements NestInterceptor {
  intercept(context: ExecutionContext, next: CallHandler): Observable<any> {
    const type = context.getType();

    if (type === 'rpc') {
      const grpcContext = context.switchToRpc();
      const metadata: Metadata = grpcContext.getContext();
      const data = grpcContext.getData();

      if (metadata && typeof metadata.get === 'function') {
        const tenantHeader = metadata.get('x-tenant-id');
        if (tenantHeader && tenantHeader.length > 0) {
          data.tenantId = String(tenantHeader[0]);
        }
      }
    }

    return next.handle();
  }
}
