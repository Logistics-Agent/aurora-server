import { PrismaService } from '../prisma/prisma.service';
import { EstimateCostRequest, EstimateCostResponse, GetCustomsDutyRequest, GetCustomsDutyResponse } from './dto/financial.dto';
export declare class FinancialService {
    private readonly prisma;
    private readonly logger;
    constructor(prisma: PrismaService);
    estimateCost(request: EstimateCostRequest, tenantId?: string): Promise<EstimateCostResponse>;
    getCustomsDuty(request: GetCustomsDutyRequest): Promise<GetCustomsDutyResponse>;
}
