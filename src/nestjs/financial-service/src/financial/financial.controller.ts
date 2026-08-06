import { Controller, Logger } from '@nestjs/common';
import { GrpcMethod } from '@nestjs/microservices';
import { Metadata, ServerUnaryCall } from '@grpc/grpc-js';
import { FinancialService } from './financial.service';
import {
  EstimateCostRequest,
  EstimateCostResponse,
  GetCustomsDutyRequest,
  GetCustomsDutyResponse,
} from './dto/financial.dto';

@Controller()
export class FinancialController {
  private readonly logger = new Logger(FinancialController.name);

  constructor(private readonly financialService: FinancialService) {}

  @GrpcMethod('FinancialService', 'EstimateCost')
  async estimateCost(
    data: EstimateCostRequest,
    metadata: Metadata,
    call: ServerUnaryCall<any, any>,
  ): Promise<EstimateCostResponse> {
    const tenantIdHeader = metadata.get('x-tenant-id');
    const tenantId = tenantIdHeader.length > 0 ? String(tenantIdHeader[0]) : undefined;

    return this.financialService.estimateCost(data, tenantId);
  }

  @GrpcMethod('FinancialService', 'GetCustomsDuty')
  async getCustomsDuty(
    data: GetCustomsDutyRequest,
    metadata: Metadata,
  ): Promise<GetCustomsDutyResponse> {
    return this.financialService.getCustomsDuty(data);
  }
}
