import { Injectable, OnModuleInit, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { ClientGrpc, Client, Transport } from '@nestjs/microservices';
import { join } from 'path';
import { existsSync } from 'fs';
import { Observable, lastValueFrom } from 'rxjs';
import { Metadata } from '@grpc/grpc-js';
import { circuitBreaker, handleAll, ConsecutiveBreaker } from 'cockatiel';

export interface FinancialEstimateCostRequest {
  tenantId?: string;
  originCountry: string;
  originPort: string;
  destinationCountry: string;
  destinationPort: string;
  weightKg: number;
  volumeCbm: number;
  transportMode?: string;
  cargoType?: string;
  cargoValue?: number;
  currency?: string;
  hsCodes?: string[];
}

export interface FinancialEstimateCostResponse {
  baseFreightCost: number;
  portHandlingFees: number;
  importDutyFee: number;
  vatFee: number;
  totalCustomsFee: number;
  totalEstimatedCost: number;
  currency: string;
  calculationMethod: string;
  description: string;
  is_estimated_fallback?: boolean;
}

interface FinancialGrpcServiceClient {
  estimateCost(data: FinancialEstimateCostRequest, metadata: Metadata): Observable<FinancialEstimateCostResponse>;
}

@Injectable()
export class FinancialGrpcClient implements OnModuleInit {
  private readonly logger = new Logger(FinancialGrpcClient.name);
  private financialGrpcService: FinancialGrpcServiceClient;

  // TASK-013: Cockatiel Circuit Breaker (opens after 3 consecutive failures, 10s half-open reset)
  private readonly breaker = circuitBreaker(handleAll, {
    halfOpenAfter: 10000,
    breaker: new ConsecutiveBreaker(3),
  });

  @Client({
    transport: Transport.GRPC,
    options: {
      package: 'financial',
      protoPath: [
        join(process.cwd(), '../../../protos/financial.proto'),
        join(process.cwd(), '../../protos/financial.proto'),
        join(__dirname, '../../../../../protos/financial.proto'),
      ].find((p) => existsSync(p)) || join(process.cwd(), '../../protos/financial.proto'),
      url: process.env.FINANCIAL_SERVICE_GRPC_URL || 'localhost:5003',
    },
  })
  private client: ClientGrpc;

  constructor(private readonly configService: ConfigService) {}

  onModuleInit() {
    this.financialGrpcService = this.client.getService<FinancialGrpcServiceClient>('FinancialService');
  }

  async estimateCost(request: FinancialEstimateCostRequest): Promise<FinancialEstimateCostResponse> {
    if (!request.tenantId) throw new Error('tenantId is required for financial estimate');
    const metadata = new Metadata();
    metadata.set('x-tenant-id', request.tenantId);
    this.logger.log(`[CircuitBreaker Call] FinancialService gRPC route ${request.originPort} -> ${request.destinationPort}`);

    try {
      return await this.breaker.execute(async () => {
        return await lastValueFrom(this.financialGrpcService.estimateCost(request, metadata));
      });
    } catch (error) {
      this.logger.error(`FinancialService estimate failed: ${error.message}`);
      throw error;
    }
  }
}

