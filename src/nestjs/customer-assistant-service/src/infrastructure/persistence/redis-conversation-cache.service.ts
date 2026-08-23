import { Injectable, Logger, OnModuleInit, OnModuleDestroy } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import Redis from 'ioredis';
import { ConversationMessage } from '../../domain/entities/message.entity';

@Injectable()
export class RedisConversationCacheService implements OnModuleInit, OnModuleDestroy {
  private readonly logger = new Logger(RedisConversationCacheService.name);
  private redisClient: Redis | null = null;
  private isConnected = false;
  private readonly defaultTtlSeconds = 86400; // 24 hours

  constructor(private readonly configService: ConfigService) {}

  onModuleInit() {
    const redisHost = this.configService.get<string>('REDIS_HOST') || 'localhost';
    const redisPort = Number(this.configService.get<number>('REDIS_PORT') || 6379);
    const redisPassword = this.configService.get<string>('REDIS_PASSWORD') || undefined;

    try {
      this.redisClient = new Redis({
        host: redisHost,
        port: redisPort,
        password: redisPassword,
        lazyConnect: true,
        maxRetriesPerRequest: 1,
        enableOfflineQueue: false,
        retryStrategy: () => null, // Do not spam reconnect on offline dev
      });

      this.redisClient.on('connect', () => {
        this.isConnected = true;
        this.logger.log(`[RedisCache] Connected to Redis at ${redisHost}:${redisPort}`);
      });

      this.redisClient.on('error', (err) => {
        this.isConnected = false;
        this.logger.debug(`[RedisCache] Redis connection notice: ${err.message}`);
      });

      this.redisClient.connect().catch(() => {
        this.isConnected = false;
        this.logger.debug('[RedisCache] Redis not available, using in-memory/postgres direct path.');
      });
    } catch (err) {
      this.isConnected = false;
      this.logger.warn(`[RedisCache] Failed to initialize Redis client: ${err}`);
    }
  }

  async onModuleDestroy() {
    if (this.redisClient) {
      try {
        await this.redisClient.quit();
      } catch {
        // ignore
      }
    }
  }

  private buildRecentKey(tenantId: string, userId: string, conversationId: string): string {
    return `chat:recent:${tenantId}:${userId}:${conversationId}`;
  }

  async getRecentMessages(
    tenantId: string,
    userId: string,
    conversationId: string,
  ): Promise<ConversationMessage[] | null> {
    if (!this.isConnected || !this.redisClient) return null;

    try {
      const key = this.buildRecentKey(tenantId, userId, conversationId);
      const raw = await this.redisClient.get(key);
      if (!raw) return null;

      const parsed = JSON.parse(raw);
      return parsed.map((m: any) => ({
        ...m,
        createdAt: new Date(m.createdAt),
      }));
    } catch (err) {
      this.logger.debug(`[RedisCache] getRecentMessages cache miss or error: ${err}`);
      return null;
    }
  }

  async setRecentMessages(
    tenantId: string,
    userId: string,
    conversationId: string,
    messages: ConversationMessage[],
  ): Promise<void> {
    if (!this.isConnected || !this.redisClient) return;

    try {
      const key = this.buildRecentKey(tenantId, userId, conversationId);
      await this.redisClient.set(
        key,
        JSON.stringify(messages),
        'EX',
        this.defaultTtlSeconds,
      );
    } catch (err) {
      this.logger.debug(`[RedisCache] setRecentMessages error: ${err}`);
    }
  }

  async invalidateRecentMessages(
    tenantId: string,
    userId: string,
    conversationId: string,
  ): Promise<void> {
    if (!this.isConnected || !this.redisClient) return;

    try {
      const key = this.buildRecentKey(tenantId, userId, conversationId);
      await this.redisClient.del(key);
    } catch (err) {
      this.logger.debug(`[RedisCache] invalidateRecentMessages error: ${err}`);
    }
  }
}
