import { Controller, UseFilters, UseInterceptors } from '@nestjs/common';
import { GrpcMethod } from '@nestjs/microservices';
import { FinancialService } from '../../application/services/financial.service';
import { TenantInterceptor } from '../../common/interceptors/tenant.interceptor';
import { GrpcExceptionFilter } from '../../common/filters/grpc-exception.filter';
import {
  EstimateCostRequest,
  EstimateCostResponse,
  GetCustomsDutyRequest,
  GetCustomsDutyResponse,
  GetMinAcceptableRateRequest,
  GetMinAcceptableRateResponse,
  GetDynamicMarginRequest,
  GetDynamicMarginResponse,
  GetExchangeRateRequest,
  GetExchangeRateResponse,
} from '../dto/financial.dto';

@Controller()
@UseInterceptors(TenantInterceptor)
@UseFilters(GrpcExceptionFilter)
export class FinancialController {
  constructor(private readonly financialService: FinancialService) {}

  @GrpcMethod('FinancialService', 'EstimateCost')
  async estimateCost(data: EstimateCostRequest): Promise<EstimateCostResponse> {
    return this.financialService.estimateCost(data, data.tenantId);
  }

  @GrpcMethod('FinancialService', 'GetCustomsDuty')
  async getCustomsDuty(data: GetCustomsDutyRequest): Promise<GetCustomsDutyResponse> {
    return this.financialService.getCustomsDuty(data, data.tenantId);
  }

  @GrpcMethod('FinancialService', 'GetMinAcceptableRate')
  async getMinAcceptableRate(data: GetMinAcceptableRateRequest): Promise<GetMinAcceptableRateResponse> {
    return this.financialService.getMinAcceptableRate(data, data.tenantId);
  }

  // ── TASK-001: Dynamic Margin Decay ──────────────────────────────────────────

  @GrpcMethod('FinancialService', 'GetDynamicMargin')
  async getDynamicMargin(data: GetDynamicMarginRequest): Promise<GetDynamicMarginResponse> {
    return this.financialService.getDynamicMargin(data, data.tenantId);
  }

  // ── TASK-002: Exchange Rate ──────────────────────────────────────────────────

  @GrpcMethod('FinancialService', 'GetExchangeRate')
  async getExchangeRate(data: GetExchangeRateRequest): Promise<GetExchangeRateResponse> {
    return this.financialService.getExchangeRate(data, data.tenantId);
  }
}
