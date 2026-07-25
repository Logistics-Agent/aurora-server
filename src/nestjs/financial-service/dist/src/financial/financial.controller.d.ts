import { Metadata, ServerUnaryCall } from '@grpc/grpc-js';
import { FinancialService } from './financial.service';
import { EstimateCostRequest, EstimateCostResponse, GetCustomsDutyRequest, GetCustomsDutyResponse } from './dto/financial.dto';
export declare class FinancialController {
    private readonly financialService;
    private readonly logger;
    constructor(financialService: FinancialService);
    estimateCost(data: EstimateCostRequest, metadata: Metadata, call: ServerUnaryCall<any, any>): Promise<EstimateCostResponse>;
    getCustomsDuty(data: GetCustomsDutyRequest, metadata: Metadata): Promise<GetCustomsDutyResponse>;
}
