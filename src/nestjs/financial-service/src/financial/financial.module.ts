import { Module } from '@nestjs/common';
import { FinancialController } from '../interface/controllers/financial.controller';
import { FinancialService } from '../application/services/financial.service';
import { CostCalculatorDomainService } from '../domain/services/cost-calculator.domain-service';
import { RateCacheService } from '../infrastructure/cache/rate-cache.service';

@Module({
  controllers: [FinancialController],
  providers: [FinancialService, CostCalculatorDomainService, RateCacheService],
  exports: [FinancialService, CostCalculatorDomainService, RateCacheService],
})
export class FinancialModule {}

