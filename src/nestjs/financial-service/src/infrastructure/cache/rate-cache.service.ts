import { Injectable, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import Redis from 'ioredis';

export interface RateCacheData {
  ratePerKg: number;
  ratePerCbm: number;
  flatFee: number;
  currency: string;
}

@Injectable()
export class RateCacheService {
  private readonly logger = new Logger(RateCacheService.name);
  private redisClient: Redis | null = null;
  private memoryCache: Map<string, RateCacheData> = new Map();

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

  /**
   * Cache a freight rate in Redis (TTL = 24 hours)
   */
  async setRate(tenantId: string, routeKey: string, data: RateCacheData): Promise<void> {
    const redisKey = `financial:rates:${tenantId}:${routeKey}`;

    if (this.redisClient) {
      try {
        await this.redisClient.set(redisKey, JSON.stringify(data), 'EX', 86400);
        this.logger.log(`[RateCache] Cached key '${redisKey}' in Redis.`);
        return;
      } catch (err) {
        this.logger.warn(`[RateCache] Redis set failed (${err.message}). Using memory fallback.`);
      }
    }

    this.memoryCache.set(redisKey, data);
  }

  /**
   * Get cached rate (< 2ms response)
   */
  async getRate(tenantId: string, routeKey: string): Promise<RateCacheData | null> {
    const redisKey = `financial:rates:${tenantId}:${routeKey}`;

    if (this.redisClient) {
      try {
        const cached = await this.redisClient.get(redisKey);
        if (cached) {
          this.logger.debug(`[RateCache HIT - Redis] Key '${redisKey}'`);
          return JSON.parse(cached);
        }
      } catch {
        // Fallthrough
      }
    }

    if (this.memoryCache.has(redisKey)) {
      this.logger.debug(`[RateCache HIT - Memory] Key '${redisKey}'`);
      return this.memoryCache.get(redisKey)!;
    }

    return null;
  }
}
