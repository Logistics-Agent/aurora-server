import { RpcExceptionFilter, ArgumentsHost, HttpException } from '@nestjs/common';
import { Observable } from 'rxjs';
import { RpcException } from '@nestjs/microservices';
export declare class GrpcExceptionFilter implements RpcExceptionFilter<RpcException | Error> {
    private readonly logger;
    catch(exception: RpcException | Error | HttpException, host: ArgumentsHost): Observable<any>;
}
