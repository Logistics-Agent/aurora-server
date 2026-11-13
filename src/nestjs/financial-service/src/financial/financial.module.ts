import { Module } from '@nestjs/common';
import { FinancialController } from '../interface/controllers/financial.controller';
import { FinancialService } from '../application/services/financial.service';
import { CostCalculatorDomainService } from '../domain/services/cost-calculator.domain-service';
import { RateCacheService } from '../infrastructure/cache/rate-cache.service';
import { ExchangeRateSyncCronJob } from '../infrastructure/jobs/exchange-rate-sync.cron';

@Module({
  controllers: [FinancialController],
  providers: [FinancialService, CostCalculatorDomainService, RateCacheService, ExchangeRateSyncCronJob],
  exports: [FinancialService, CostCalculatorDomainService, RateCacheService],
})
export class FinancialModule {}

