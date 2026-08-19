import { IoAdapter } from '@nestjs/platform-socket.io';
import { ServerOptions } from 'socket.io';
import { createAdapter } from '@socket.io/redis-adapter';
import Redis from 'ioredis';
import { Logger } from '@nestjs/common';

export class RedisIoAdapter extends IoAdapter {
  private adapterConstructor: ReturnType<typeof createAdapter>;
  private readonly logger = new Logger(RedisIoAdapter.name);

  async connectToRedis(host: string, port: number, password?: string): Promise<boolean> {
    const redisOptions = {
      host,
      port,
      password: password || undefined,
      lazyConnect: true,
      maxRetriesPerRequest: 0,
      enableOfflineQueue: false,
      connectTimeout: 1000,
      retryStrategy: () => null, // Disable reconnection retries when Redis is not running
    };

    let pubClient: Redis | null = null;
    let subClient: Redis | null = null;

    try {
      pubClient = new Redis(redisOptions);
      // Silence background error emitter
      pubClient.on('error', () => {});
      await pubClient.connect();

      subClient = new Redis(redisOptions);
      subClient.on('error', () => {});
      await subClient.connect();

      this.adapterConstructor = createAdapter(pubClient, subClient);
      this.logger.log(`[RedisIoAdapter] Successfully connected to Redis at ${host}:${port} for Horizontal Scaling.`);
      return true;
    } catch (error) {
      this.logger.warn(
        `[RedisIoAdapter] Local Redis is offline at ${host}:${port}. Operating in Single-Node In-Memory Mode cleanly.`,
      );

      if (pubClient) {
        try { pubClient.disconnect(); } catch (e) {}
      }
      if (subClient) {
        try { subClient.disconnect(); } catch (e) {}
      }

      this.adapterConstructor = undefined;
      return false;
    }
  }

  createIOServer(port: number, options?: ServerOptions): any {
    const server = super.createIOServer(port, options);
    if (this.adapterConstructor) {
      server.adapter(this.adapterConstructor);
    }
    return server;
  }
}
