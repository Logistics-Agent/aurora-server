import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { TerminusModule } from '@nestjs/terminus';
import configuration from './config/configuration';
import { validate } from './config/env.validation';
import { EventsGateway } from './gateway/events.gateway';
import { WsJwtGuard } from './common/guards/ws-jwt.guard';
import { MQConsumerService } from './messaging/mq-consumer.service';
import { OfflineBufferService } from './messaging/offline-buffer.service';
import { HealthController } from './health/health.controller';

@Module({
  imports: [
    ConfigModule.forRoot({
      isGlobal: true,
      load: [configuration],
      validate,
    }),
    TerminusModule,
  ],
  controllers: [HealthController],
  providers: [EventsGateway, WsJwtGuard, MQConsumerService, OfflineBufferService],
})
export class AppModule {}


