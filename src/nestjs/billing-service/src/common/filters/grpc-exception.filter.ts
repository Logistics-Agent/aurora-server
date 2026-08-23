import {
  Catch,
  RpcExceptionFilter,
  ArgumentsHost,
  HttpException,
  HttpStatus,
  Logger,
} from '@nestjs/common';
import { Observable, throwError } from 'rxjs';
import { RpcException } from '@nestjs/microservices';
import { status } from '@grpc/grpc-js';

@Catch()
export class GrpcExceptionFilter implements RpcExceptionFilter<RpcException | Error> {
  private readonly logger = new Logger(GrpcExceptionFilter.name);

  catch(exception: RpcException | Error | HttpException, host: ArgumentsHost): Observable<any> {
    this.logger.error(`Exception caught in gRPC filter: ${exception.message}`, (exception as any).stack);

    let statusCode: status = status.INTERNAL;
    let message = exception.message || 'Internal server error';

    if (exception instanceof RpcException) {
      return throwError(() => exception);
    }

    if (exception instanceof HttpException) {
      const httpStatus = exception.getStatus();
      switch (httpStatus) {
        case HttpStatus.BAD_REQUEST:
          statusCode = status.INVALID_ARGUMENT;
          break;
        case HttpStatus.NOT_FOUND:
          statusCode = status.NOT_FOUND;
          break;
        case HttpStatus.CONFLICT:
          statusCode = status.ALREADY_EXISTS;
          break;
        case HttpStatus.UNAUTHORIZED:
        case HttpStatus.FORBIDDEN:
          statusCode = status.PERMISSION_DENIED;
          break;
        default:
          statusCode = status.INTERNAL;
      }
      message = exception.message;
    }

    return throwError(() => new RpcException({ code: statusCode, message }));
  }
}
