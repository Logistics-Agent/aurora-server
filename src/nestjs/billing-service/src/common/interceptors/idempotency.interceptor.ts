import {
  Injectable,
  NestInterceptor,
  ExecutionContext,
  CallHandler,
  Logger,
} from '@nestjs/common';
import { Observable, of } from 'rxjs';
import { tap } from 'rxjs/operators';
import { Metadata } from '@grpc/grpc-js';
import { ConfigService } from '@nestjs/config';
import Redis from 'ioredis';

@Injectable()
export class IdempotencyInterceptor implements NestInterceptor {
  private readonly logger = new Logger(IdempotencyInterceptor.name);
  private redisClient: Redis | null = null;
  private inMemoryCache: Map<string, { data: any; expiresAt: number }> = new Map();

  constructor(private readonly configService: ConfigService) {
    const host = this.configService.get<string>('redis.host', 'localhost');
    const port = this.configService.get<number>('redis.port', 6379);

    try {
      this.redisClient = new Redis({
        host,
        port,
        maxRetriesPerRequest: 1,
        lazyConnect: true,
      });
      this.redisClient.connect().catch(() => {
        this.redisClient = null;
      });
    } catch {
      this.redisClient = null;
    }
  }

  async intercept(context: ExecutionContext, next: CallHandler): Promise<Observable<any>> {
    let idempotencyKey: string | null = null;
    let tenantId = 'a0000000-0000-0000-0000-000000000001';

    const type = context.getType();

    if (type === 'rpc') {
      const grpcContext = context.switchToRpc();
      const metadata: Metadata = grpcContext.getContext();
      const data = grpcContext.getData();

      if (data && data.tenantId) {
        tenantId = data.tenantId;
      }

      if (metadata && typeof metadata.get === 'function') {
        const keyHeader = metadata.get('x-idempotency-key');
        if (keyHeader && keyHeader.length > 0) {
          idempotencyKey = String(keyHeader[0]);
        }
      }

      if (!idempotencyKey && data && data.idempotencyKey) {
        idempotencyKey = String(data.idempotencyKey);
      }
    } else if (type === 'http') {
      const req = context.switchToHttp().getRequest();
      idempotencyKey = req.headers['x-idempotency-key'] || req.headers['idempotency-key'];
      tenantId = req.headers['x-tenant-id'] || tenantId;
    }

    // Nếu không truyền x-idempotency-key -> Bỏ qua check idempotency, tiếp tục xử lý
    if (!idempotencyKey) {
      return next.handle();
    }

    const redisKey = `idempotency:${tenantId}:${idempotencyKey}`;

    // 1. Check Redis / In-Memory Cache
    if (this.redisClient) {
      try {
        const cached = await this.redisClient.get(redisKey);
        if (cached) {
          this.logger.log(`[Idempotency HIT - Redis] Key '${redisKey}' -> Returning cached response.`);
          return of(JSON.parse(cached));
        }
      } catch (err) {
        this.logger.warn(`[Idempotency] Redis get failed (${err.message}). Checking in-memory.`);
      }
    }

    // In-memory fallback check
    const localCached = this.inMemoryCache.get(redisKey);
    if (localCached && localCached.expiresAt > Date.now()) {
      this.logger.log(`[Idempotency HIT - Memory] Key '${redisKey}' -> Returning cached response.`);
      return of(localCached.data);
    }

    // 2. Execute and cache response with TTL = 120s
    return next.handle().pipe(
      tap(async (response) => {
        const ttlSeconds = 120;
        if (this.redisClient) {
          try {
            await this.redisClient.set(redisKey, JSON.stringify(response), 'EX', ttlSeconds);
            this.logger.log(`[Idempotency SET - Redis] Cached key '${redisKey}' for ${ttlSeconds}s`);
            return;
          } catch {
            // Fallthrough to in-memory
          }
        }

        this.inMemoryCache.set(redisKey, {
          data: response,
          expiresAt: Date.now() + ttlSeconds * 1000,
        });
        this.logger.log(`[Idempotency SET - Memory] Cached key '${redisKey}' for ${ttlSeconds}s`);
      }),
    );
  }
}
