import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { ScheduleModule } from '@nestjs/schedule';
import { TerminusModule } from '@nestjs/terminus';
import configuration from './config/configuration';
import { validate } from './config/env.validation';
import { PrismaModule } from './infrastructure/prisma/prisma.module';
import { BillingModule } from './billing/billing.module';
import { HealthController } from './health/health.controller';

@Module({
  imports: [
    ConfigModule.forRoot({
      isGlobal: true,
      load: [configuration],
      validate,
    }),
    ScheduleModule.forRoot(),
    TerminusModule,
    PrismaModule,
    BillingModule,
  ],
  controllers: [HealthController],
})
export class AppModule {}


