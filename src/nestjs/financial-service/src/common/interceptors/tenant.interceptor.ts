import {
  BadRequestException,
  Injectable,
  NestInterceptor,
  ExecutionContext,
  CallHandler,
} from '@nestjs/common';
import { Observable } from 'rxjs';
import { Metadata } from '@grpc/grpc-js';
import { status } from '@grpc/grpc-js';
import { RpcException } from '@nestjs/microservices';

@Injectable()
export class TenantInterceptor implements NestInterceptor {
  intercept(context: ExecutionContext, next: CallHandler): Observable<any> {
    const type = context.getType();

    if (type === 'rpc') {
      const grpcContext = context.switchToRpc();
      const metadata: Metadata = grpcContext.getContext();
      const data = grpcContext.getData();

      const tenantHeader = metadata && typeof metadata.get === 'function'
        ? metadata.get('x-tenant-id') : [];
      if (!tenantHeader || tenantHeader.length === 0 || !String(tenantHeader[0]).trim()) {
        throw new RpcException({ code: status.UNAUTHENTICATED, message: 'x-tenant-id metadata is required' });
      }
      data.tenantId = String(tenantHeader[0]).trim();
    } else if (type === 'http') {
      const req = context.switchToHttp().getRequest();
      const rawHeader = req?.headers?.['x-tenant-id'];
      const tenantHeader = Array.isArray(rawHeader) ? rawHeader[0] : rawHeader;
      if (!tenantHeader || !String(tenantHeader).trim()) {
        throw new BadRequestException('x-tenant-id header is required');
      }
      req.tenantId = String(tenantHeader).trim();
    }

    return next.handle();
  }
}
