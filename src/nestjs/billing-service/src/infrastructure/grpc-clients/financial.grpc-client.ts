import { Injectable, OnModuleInit, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { ClientGrpc, Client, Transport } from '@nestjs/microservices';
import { join } from 'path';
import { existsSync } from 'fs';
import { Observable, lastValueFrom } from 'rxjs';
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
  estimateCost(data: FinancialEstimateCostRequest): Observable<FinancialEstimateCostResponse>;
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
    this.logger.log(`[CircuitBreaker Call] FinancialService gRPC route ${request.originPort} -> ${request.destinationPort}`);

    try {
      return await this.breaker.execute(async () => {
        return await lastValueFrom(this.financialGrpcService.estimateCost(request));
      });
    } catch (error) {
      this.logger.warn(`[CircuitBreaker OPEN / Fallback] FinancialService call failed (${error.message}). Returning Last-Known-Good rate.`);
      return {
        baseFreightCost: 1500.0,
        portHandlingFees: 300.0,
        importDutyFee: 90.0,
        vatFee: 45.0,
        totalCustomsFee: 135.0,
        totalEstimatedCost: 1935.0,
        currency: 'USD',
        calculationMethod: 'COCKATIEL_CIRCUIT_BREAKER_FALLBACK',
        description: 'Fallback rate used during circuit breaker trip or service degradation',
        is_estimated_fallback: true,
      };
    }
  }
}

