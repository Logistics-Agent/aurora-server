import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';
import { Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { RedisIoAdapter } from './common/adapters/redis-io.adapter';

async function bootstrap() {
  const logger = new Logger('RealtimeHubBootstrap');
  const app = await NestFactory.create(AppModule);
  const configService = app.get(ConfigService);

  const port = configService.get<number>('websocket.port', 5005);
  const redisHost = configService.get<string>('redis.host', 'localhost');
  const redisPort = configService.get<number>('redis.port', 6379);
  const redisPassword = configService.get<string>('redis.password');
  const corsOrigin = configService.get<string>('websocket.corsOrigin', '*');

  app.enableCors({
    origin: corsOrigin,
    credentials: true,
  });

  // Enable RedisIoAdapter for Horizontal Scaling if Redis is available
  const redisIoAdapter = new RedisIoAdapter(app);
  const isRedisConnected = await redisIoAdapter.connectToRedis(redisHost, redisPort, redisPassword);
  if (isRedisConnected) {
    app.useWebSocketAdapter(redisIoAdapter);
  }

  await app.listen(port);
  logger.log(`🚀 Realtime Hub Service running on port ${port} (Namespace: / & /realtime)`);
}

bootstrap();
