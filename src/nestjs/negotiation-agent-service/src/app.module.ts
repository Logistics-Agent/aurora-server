import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { TerminusModule } from '@nestjs/terminus';
import { PrismaModule } from './infrastructure/prisma/prisma.module';
import { NegotiationStrategyDomainService } from './domain/services/negotiation-strategy.domain-service';
import { GeminiAIClient } from './infrastructure/ai/gemini.client';
import { NegotiationService } from './application/services/negotiation.service';
import { NegotiationController } from './interface/controllers/negotiation.controller';
import { HealthController } from './health/health.controller';

@Module({
  imports: [
    ConfigModule.forRoot({
      isGlobal: true,
    }),
    TerminusModule,
    PrismaModule,
  ],
  controllers: [NegotiationController, HealthController],
  providers: [
    NegotiationStrategyDomainService,
    GeminiAIClient,
    NegotiationService,
  ],
})
export class AppModule {}
