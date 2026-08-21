import { Module } from '@nestjs/common';
import { FinancialController } from '../interface/controllers/financial.controller';
import { FinancialService } from '../application/services/financial.service';
import { CostCalculatorDomainService } from '../domain/services/cost-calculator.domain-service';
import { ExchangeRateSyncCronJob } from '../infrastructure/jobs/exchange-rate-sync.cron';
import { RateCacheService } from '../infrastructure/cache/rate-cache.service';

@Module({
  controllers: [FinancialController],
  providers: [FinancialService, CostCalculatorDomainService, ExchangeRateSyncCronJob, RateCacheService],
  exports: [FinancialService, CostCalculatorDomainService, RateCacheService],
})
export class FinancialModule {}

