import { Body, Controller, Get, Param, Patch, Post, Query, Req, UseFilters, UseInterceptors } from '@nestjs/common';
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
  CommercialQuoteDto,
  CreateQuoteRequest,
  ListQuotesRequest,
  ListQuotesResponse,
  RegisterShipmentThreadRequest,
  ConfirmQuoteRequest,
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

  @GrpcMethod('FinancialService', 'CreateQuote')
  async createQuoteGrpc(data: CreateQuoteRequest): Promise<CommercialQuoteDto> {
    return this.financialService.createQuote(data, data.tenantId);
  }

  @GrpcMethod('FinancialService', 'ListQuotes')
  async listQuotesGrpc(data: ListQuotesRequest): Promise<ListQuotesResponse> {
    return this.financialService.listQuotes(data, data.tenantId);
  }

  @GrpcMethod('FinancialService', 'RegisterShipmentThread')
  async registerShipmentThreadGrpc(data: RegisterShipmentThreadRequest): Promise<CommercialQuoteDto> {
    return this.financialService.registerShipmentThread(data, data.tenantId);
  }

  @GrpcMethod('FinancialService', 'ConfirmQuote')
  async confirmQuoteGrpc(data: ConfirmQuoteRequest): Promise<CommercialQuoteDto> {
    return this.financialService.confirmQuote(data, data.tenantId);
  }

  @Post('api/v1/financial/quotes/register-shipment')
  async registerShipmentThreadHttp(
    @Body() body: RegisterShipmentThreadRequest,
    @Req() req: any,
  ): Promise<CommercialQuoteDto> {
    return this.financialService.registerShipmentThread(
      body,
      req?.tenantId || req?.headers?.['x-tenant-id'] || body?.tenantId,
    );
  }

  @Post('api/v1/financial/quotes')
  async createQuoteHttp(@Body() body: CreateQuoteRequest, @Req() req: any): Promise<CommercialQuoteDto> {
    return this.financialService.createQuote(
      body,
      req?.tenantId || req?.headers?.['x-tenant-id'] || body?.tenantId,
    );
  }

  @Get('api/v1/financial/quotes')
  async listQuotesHttp(@Req() req: any, @Query() query: Record<string, any>): Promise<ListQuotesResponse> {
    return this.financialService.listQuotes(
      {
        sourceThreadId: query?.threadId || query?.sourceThreadId,
        shipmentId: query?.shipmentId,
        quoteId: query?.quoteId,
      },
      req?.tenantId || req?.headers?.['x-tenant-id'],
    );
  }

  @Patch('api/v1/financial/quotes/:quoteId/confirm')
  async confirmQuoteHttp(
    @Param('quoteId') quoteId: string,
    @Body() body: { agreedPrice: number; shipmentId?: string },
    @Req() req: any,
  ): Promise<CommercialQuoteDto> {
    return this.financialService.confirmQuote(
      {
        quoteId,
        shipmentId: body?.shipmentId,
        agreedPrice: body?.agreedPrice,
      },
      req?.tenantId || req?.headers?.['x-tenant-id'],
    );
  }

  @Get('api/v1/financial/quotes/:shipmentId')
  async getShipmentQuote(
    @Param('shipmentId') shipmentId: string,
    @Req() req: any,
    @Query() query: Record<string, any>,
  ) {
    return this.financialService.getShipmentQuote(
      shipmentId,
      req?.tenantId || req?.headers?.['x-tenant-id'],
      query,
    );
  }
}
