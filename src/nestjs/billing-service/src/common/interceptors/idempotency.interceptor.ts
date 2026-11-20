import {
  ConflictException,
  CallHandler,
  ExecutionContext,
  Injectable,
  Logger,
  NestInterceptor,
  OnModuleDestroy,
} from '@nestjs/common';
import { Observable, from, of, throwError } from 'rxjs';
import { catchError, mergeMap, tap } from 'rxjs/operators';
import { Metadata } from '@grpc/grpc-js';
import { ConfigService } from '@nestjs/config';
import Redis from 'ioredis';

const IN_PROGRESS_MARKER = '__IN_PROGRESS__';
const LOCK_TTL_SECONDS = 30;
const RESPONSE_TTL_SECONDS = 120;

interface CachedIdempotencyEntry {
  status: 'IN_PROGRESS' | 'COMPLETED';
  data: any;
  expiresAt: number;
}

@Injectable()
export class IdempotencyInterceptor implements NestInterceptor, OnModuleDestroy {
  private readonly logger = new Logger(IdempotencyInterceptor.name);
  private redisClient: Redis | null = null;
  private inMemoryCache: Map<string, CachedIdempotencyEntry> = new Map();

  constructor(private readonly configService: ConfigService) {
    const disabled = this.configService.get<boolean>('redis.disabled', false);
    if (disabled) {
      return;
    }

    const host = this.configService.get<string>('redis.host', 'localhost');
    const port = this.configService.get<number>('redis.port', 6379);
    const password =
      this.configService.get<string>('redis.password') || process.env.REDIS_PASSWORD || undefined;

    try {
      this.redisClient = new Redis({
        host,
        port,
        password,
        maxRetriesPerRequest: 1,
        connectTimeout: 1000,
        lazyConnect: true,
      });
      this.redisClient.on('error', () => {
        // Handled via catch fallback to inMemoryCache
      });
      this.redisClient.connect().catch(() => {
        this.redisClient = null;
      });
    } catch {
      this.redisClient = null;
    }
  }

  async onModuleDestroy(): Promise<void> {
    if (this.redisClient) {
      try {
        await this.redisClient.quit();
      } catch {
        this.redisClient.disconnect();
      }
      this.redisClient = null;
    }
  }

  async intercept(context: ExecutionContext, next: CallHandler): Promise<Observable<any>> {
    let rawIdempotencyKey: string | string[] | null = null;
    let rawTenantId: string | string[] = 'a0000000-0000-0000-0000-000000000001';

    const type = context.getType();

    if (type === 'rpc') {
      const grpcContext = context.switchToRpc();
      const metadata: Metadata = grpcContext.getContext();
      const data = grpcContext.getData();

      if (data && data.tenantId) {
        rawTenantId = data.tenantId;
      } else if (metadata && typeof metadata.get === 'function') {
        const tenantHeader = metadata.get('x-tenant-id');
        if (tenantHeader && tenantHeader.length > 0) {
          rawTenantId = String(tenantHeader[0]);
        }
      }

      if (metadata && typeof metadata.get === 'function') {
        const keyHeader = metadata.get('x-idempotency-key');
        if (keyHeader && keyHeader.length > 0) {
          rawIdempotencyKey = String(keyHeader[0]);
        }
      }

      if (!rawIdempotencyKey && data && data.idempotencyKey) {
        rawIdempotencyKey = String(data.idempotencyKey);
      }
    } else if (type === 'http') {
      const req = context.switchToHttp().getRequest();
      rawIdempotencyKey = req.headers?.['x-idempotency-key'] || req.headers?.['idempotency-key'] || null;
      rawTenantId =
        req.headers?.['x-tenant-id'] ||
        req.body?.tenantId ||
        req.user?.tenantId ||
        rawTenantId;
    }

    const idempotencyKey = (Array.isArray(rawIdempotencyKey) ? rawIdempotencyKey[0] : rawIdempotencyKey)?.trim();
    const tenantId =
      (Array.isArray(rawTenantId) ? rawTenantId[0] : rawTenantId)?.trim() ||
      'a0000000-0000-0000-0000-000000000001';

    // Nếu không truyền x-idempotency-key -> Bỏ qua check idempotency, tiếp tục xử lý
    if (!idempotencyKey) {
      return next.handle();
    }

    const redisKey = `idempotency:${tenantId}:${idempotencyKey}`;
    let usedRedisLock = false;

    // 1. Atomic check-and-lock via Redis NX EX (fallback to atomic in-memory lock)
    if (this.redisClient) {
      try {
        const lockResult = await this.redisClient.set(
          redisKey,
          IN_PROGRESS_MARKER,
          'EX',
          LOCK_TTL_SECONDS,
          'NX',
        );
        if (lockResult === 'OK') {
          usedRedisLock = true;
        } else {
          const existing = await this.redisClient.get(redisKey);
          if (!existing || existing === IN_PROGRESS_MARKER) {
            throw new ConflictException('Request with this idempotency key is already in progress');
          }
          this.logger.log(`[Idempotency HIT - Redis] Key '${redisKey}' -> Returning cached response.`);
          return of(JSON.parse(existing));
        }
      } catch (err) {
        if (err instanceof ConflictException) {
          throw err;
        }
        this.logger.warn(
          `[Idempotency] Redis atomic lock failed (${(err as Error).message}). Using in-memory fallback.`,
        );
      }
    }

    if (!usedRedisLock) {
      const localCached = this.inMemoryCache.get(redisKey);
      if (localCached && localCached.expiresAt > Date.now()) {
        if (localCached.status === 'IN_PROGRESS') {
          throw new ConflictException('Request with this idempotency key is already in progress');
        }
        this.logger.log(`[Idempotency HIT - Memory] Key '${redisKey}' -> Returning cached response.`);
        return of(localCached.data);
      }

      this.inMemoryCache.set(redisKey, {
        status: 'IN_PROGRESS',
        data: null,
        expiresAt: Date.now() + LOCK_TTL_SECONDS * 1000,
      });
    }

    // 2. Execute handler, persist result with 120s TTL on success, clear lock on failure
    return next.handle().pipe(
      tap(async (response) => {
        if (usedRedisLock && this.redisClient) {
          try {
            await this.redisClient.set(redisKey, JSON.stringify(response), 'EX', RESPONSE_TTL_SECONDS);
            this.logger.log(`[Idempotency SET - Redis] Cached key '${redisKey}' for ${RESPONSE_TTL_SECONDS}s`);
            return;
          } catch {
            // Fallthrough to in-memory
          }
        }

        this.inMemoryCache.set(redisKey, {
          status: 'COMPLETED',
          data: response,
          expiresAt: Date.now() + RESPONSE_TTL_SECONDS * 1000,
        });
        this.logger.log(`[Idempotency SET - Memory] Cached key '${redisKey}' for ${RESPONSE_TTL_SECONDS}s`);
      }),
      catchError((err) => {
        this.inMemoryCache.delete(redisKey);
        if (usedRedisLock && this.redisClient) {
          return from(this.redisClient.del(redisKey).catch(() => undefined)).pipe(
            mergeMap(() => throwError(() => err)),
          );
        }
        return throwError(() => err);
      }),
    );
  }
}
