"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var __metadata = (this && this.__metadata) || function (k, v) {
    if (typeof Reflect === "object" && typeof Reflect.metadata === "function") return Reflect.metadata(k, v);
};
var FinancialGrpcClient_1;
Object.defineProperty(exports, "__esModule", { value: true });
exports.FinancialGrpcClient = void 0;
const common_1 = require("@nestjs/common");
const config_1 = require("@nestjs/config");
const microservices_1 = require("@nestjs/microservices");
const path_1 = require("path");
const fs_1 = require("fs");
const rxjs_1 = require("rxjs");
const cockatiel_1 = require("cockatiel");
let FinancialGrpcClient = FinancialGrpcClient_1 = class FinancialGrpcClient {
    constructor(configService) {
        this.configService = configService;
        this.logger = new common_1.Logger(FinancialGrpcClient_1.name);
        this.breaker = (0, cockatiel_1.circuitBreaker)(cockatiel_1.handleAll, {
            halfOpenAfter: 10000,
            breaker: new cockatiel_1.ConsecutiveBreaker(3),
        });
    }
    onModuleInit() {
        this.financialGrpcService = this.client.getService('FinancialService');
    }
    async estimateCost(request) {
        this.logger.log(`[CircuitBreaker Call] FinancialService gRPC route ${request.originPort} -> ${request.destinationPort}`);
        try {
            return await this.breaker.execute(async () => {
                return await (0, rxjs_1.lastValueFrom)(this.financialGrpcService.estimateCost(request));
            });
        }
        catch (error) {
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
};
exports.FinancialGrpcClient = FinancialGrpcClient;
__decorate([
    (0, microservices_1.Client)({
        transport: microservices_1.Transport.GRPC,
        options: {
            package: 'financial',
            protoPath: [
                (0, path_1.join)(process.cwd(), '../../../protos/financial.proto'),
                (0, path_1.join)(process.cwd(), '../../protos/financial.proto'),
                (0, path_1.join)(__dirname, '../../../../../protos/financial.proto'),
            ].find((p) => (0, fs_1.existsSync)(p)) || (0, path_1.join)(process.cwd(), '../../protos/financial.proto'),
            url: process.env.FINANCIAL_SERVICE_GRPC_URL || 'localhost:5003',
        },
    }),
    __metadata("design:type", Object)
], FinancialGrpcClient.prototype, "client", void 0);
exports.FinancialGrpcClient = FinancialGrpcClient = FinancialGrpcClient_1 = __decorate([
    (0, common_1.Injectable)(),
    __metadata("design:paramtypes", [config_1.ConfigService])
], FinancialGrpcClient);
//# sourceMappingURL=financial.grpc-client.js.map