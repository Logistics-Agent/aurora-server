import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { TerminusModule } from '@nestjs/terminus';
import { PrismaModule } from './infrastructure/prisma/prisma.module';
import { NegotiationStrategyDomainService } from './domain/services/negotiation-strategy.domain-service';
import { AiGovernanceNegotiationClient } from './infrastructure/grpc/ai-governance.grpc-client';
import { NegotiationService } from './application/services/negotiation.service';
import { NegotiationController } from './interface/controllers/negotiation.controller';
import { HealthController } from './health/health.controller';
import { TenantInterceptor } from './infrastructure/security/tenant.interceptor';
import { BillingQuoteGrpcClient } from './infrastructure/grpc/billing-quote.grpc-client';

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
    AiGovernanceNegotiationClient,
    BillingQuoteGrpcClient,
    NegotiationService,
    TenantInterceptor,
  ],
  exports: [NegotiationService, NegotiationStrategyDomainService, AiGovernanceNegotiationClient],
})
export class AppModule {}
