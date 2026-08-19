import { Injectable, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import Redis from 'ioredis';

export interface BufferedMessage {
  msgId: string;
  tenantId: string;
  userId: string;
  event: string;
  payload: any;
  timestamp: number;
}

@Injectable()
export class OfflineBufferService {
  private readonly logger = new Logger(OfflineBufferService.name);
  private redisClient: Redis | null = null;
  private inMemoryBuffer: Map<string, BufferedMessage[]> = new Map(); // Fallback in-memory buffer

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
        this.logger.warn('[OfflineBufferService] Redis connection unavailable. Using In-Memory buffer mode.');
        this.redisClient = null;
      });
    } catch {
      this.redisClient = null;
    }
  }

  /**
   * Buffer a message when client ACK times out or client is offline
   */
  async bufferMessage(msg: BufferedMessage): Promise<void> {
    const key = `stream:offline_msg:${msg.tenantId}:${msg.userId}`;

    if (this.redisClient) {
      try {
        await this.redisClient.rpush(key, JSON.stringify(msg));
        await this.redisClient.expire(key, 86400 * 7); // TTL 7 days
        this.logger.log(`[OfflineBuffer] Buffered message ${msg.msgId} to Redis key '${key}'`);
        return;
      } catch (err) {
        this.logger.warn(`[OfflineBuffer] Redis rpush failed (${err.message}). Falling back to In-Memory.`);
      }
    }

    // In-memory fallback
    const list = this.inMemoryBuffer.get(key) || [];
    list.push(msg);
    this.inMemoryBuffer.set(key, list);
    this.logger.log(`[OfflineBuffer] Buffered message ${msg.msgId} to In-Memory buffer key '${key}'`);
  }

  /**
   * Flush all buffered offline messages for a user when they reconnect
   */
  async flush(tenantId: string, userId: string): Promise<BufferedMessage[]> {
    const key = `stream:offline_msg:${tenantId}:${userId}`;

    if (this.redisClient) {
      try {
        const items = await this.redisClient.lrange(key, 0, -1);
        if (items && items.length > 0) {
          await this.redisClient.del(key);
          this.logger.log(`[OfflineBuffer] Flushed ${items.length} message(s) from Redis key '${key}'`);
          return items.map((i) => JSON.parse(i));
        }
      } catch (err) {
        this.logger.warn(`[OfflineBuffer] Redis lrange failed (${err.message}). Checking In-Memory.`);
      }
    }

    // In-memory fallback
    const items = this.inMemoryBuffer.get(key) || [];
    if (items.length > 0) {
      this.inMemoryBuffer.delete(key);
      this.logger.log(`[OfflineBuffer] Flushed ${items.length} message(s) from In-Memory key '${key}'`);
    }
    return items;
  }
}
