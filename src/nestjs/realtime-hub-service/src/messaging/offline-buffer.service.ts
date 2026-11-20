import { Injectable, Logger, OnModuleDestroy } from '@nestjs/common';
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
export class OfflineBufferService implements OnModuleDestroy {
  private static readonly MAX_OFFLINE_BUFFER = 200;
  private readonly logger = new Logger(OfflineBufferService.name);
  private redisClient: Redis | null = null;
  private inMemoryBuffer: Map<string, BufferedMessage[]> = new Map(); // Fallback in-memory buffer
  private inMemoryPendingAcks: Map<string, { tenantId: string; userId: string }> = new Map();

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

      this.redisClient.on('error', () => {
        // Handled cleanly via catch fallback to in-memory buffer
      });

      this.redisClient.connect().catch(() => {
        this.logger.warn('[OfflineBufferService] Redis connection unavailable. Using In-Memory buffer mode.');
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

  /**
   * Register a distributed pending ACK key in Redis so any instance in a multi-pod cluster
   * can confirm the client's ACK and prevent duplicate offline buffering.
   */
  async markPendingAck(msgId: string, tenantId: string, userId: string, ttlSeconds = 15): Promise<void> {
    const key = `realtime:pending_ack:${msgId}`;
    const val = JSON.stringify({ tenantId, userId });
    if (this.redisClient) {
      try {
        await this.redisClient.set(key, val, 'EX', ttlSeconds);
        return;
      } catch {
        // Fallback to in-memory
      }
    }
    this.inMemoryPendingAcks.set(msgId, { tenantId, userId });
  }

  /**
   * Confirm ACK across all instances by deleting the distributed Redis pending ACK key
   */
  async confirmAck(msgId: string, tenantId: string, userIds: string[]): Promise<boolean> {
    const key = `realtime:pending_ack:${msgId}`;
    if (this.redisClient) {
      try {
        const raw = await this.redisClient.get(key);
        if (raw) {
          const parsed = JSON.parse(raw) as { tenantId: string; userId: string };
          if (parsed.tenantId === tenantId && userIds.includes(parsed.userId)) {
            await this.redisClient.del(key);
            this.inMemoryPendingAcks.delete(msgId);
            return true;
          }
        }
      } catch {
        // Fallback to in-memory
      }
    }
    const local = this.inMemoryPendingAcks.get(msgId);
    if (local && local.tenantId === tenantId && userIds.includes(local.userId)) {
      this.inMemoryPendingAcks.delete(msgId);
      return true;
    }
    return false;
  }

  /**
   * Check whether a msgId is still awaiting ACK across the cluster
   */
  async isStillPendingAck(msgId: string): Promise<boolean> {
    const key = `realtime:pending_ack:${msgId}`;
    if (this.redisClient) {
      try {
        const exists = await this.redisClient.exists(key);
        await this.redisClient.del(key);
        this.inMemoryPendingAcks.delete(msgId);
        return exists === 1;
      } catch {
        // Fallback to in-memory
      }
    }
    const hadLocal = this.inMemoryPendingAcks.has(msgId);
    this.inMemoryPendingAcks.delete(msgId);
    return hadLocal;
  }

  /**
   * Buffer a message when client ACK times out or client is offline
   */
  async bufferMessage(msg: BufferedMessage): Promise<void> {
    const key = `stream:offline_msg:${msg.tenantId}:${msg.userId}`;

    if (this.redisClient) {
      try {
        await this.redisClient.rpush(key, JSON.stringify(msg));
        await this.redisClient.ltrim(key, -OfflineBufferService.MAX_OFFLINE_BUFFER, -1);
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
    const bounded =
      list.length > OfflineBufferService.MAX_OFFLINE_BUFFER
        ? list.slice(-OfflineBufferService.MAX_OFFLINE_BUFFER)
        : list;
    this.inMemoryBuffer.set(key, bounded);
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
