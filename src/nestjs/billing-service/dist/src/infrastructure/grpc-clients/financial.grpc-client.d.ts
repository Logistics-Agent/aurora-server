import { OnModuleInit } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
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
export declare class FinancialGrpcClient implements OnModuleInit {
    private readonly configService;
    private readonly logger;
    private financialGrpcService;
    private readonly breaker;
    private client;
    constructor(configService: ConfigService);
    onModuleInit(): void;
    estimateCost(request: FinancialEstimateCostRequest): Promise<FinancialEstimateCostResponse>;
}
