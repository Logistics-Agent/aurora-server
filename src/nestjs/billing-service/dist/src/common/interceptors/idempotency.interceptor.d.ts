import { NestInterceptor, ExecutionContext, CallHandler } from '@nestjs/common';
import { Observable } from 'rxjs';
import { ConfigService } from '@nestjs/config';
export declare class IdempotencyInterceptor implements NestInterceptor {
    private readonly configService;
    private readonly logger;
    private redisClient;
    private inMemoryCache;
    constructor(configService: ConfigService);
    intercept(context: ExecutionContext, next: CallHandler): Promise<Observable<any>>;
}
