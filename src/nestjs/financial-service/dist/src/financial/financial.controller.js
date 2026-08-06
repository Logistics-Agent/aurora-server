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
var FinancialController_1;
Object.defineProperty(exports, "__esModule", { value: true });
exports.FinancialController = void 0;
const common_1 = require("@nestjs/common");
const microservices_1 = require("@nestjs/microservices");
const grpc_js_1 = require("@grpc/grpc-js");
const financial_service_1 = require("./financial.service");
let FinancialController = FinancialController_1 = class FinancialController {
    constructor(financialService) {
        this.financialService = financialService;
        this.logger = new common_1.Logger(FinancialController_1.name);
    }
    async estimateCost(data, metadata, call) {
        const tenantIdHeader = metadata.get('x-tenant-id');
        const tenantId = tenantIdHeader.length > 0 ? String(tenantIdHeader[0]) : undefined;
        return this.financialService.estimateCost(data, tenantId);
    }
    async getCustomsDuty(data, metadata) {
        return this.financialService.getCustomsDuty(data);
    }
};
exports.FinancialController = FinancialController;
__decorate([
    (0, microservices_1.GrpcMethod)('FinancialService', 'EstimateCost'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object, grpc_js_1.Metadata, Object]),
    __metadata("design:returntype", Promise)
], FinancialController.prototype, "estimateCost", null);
__decorate([
    (0, microservices_1.GrpcMethod)('FinancialService', 'GetCustomsDuty'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object, grpc_js_1.Metadata]),
    __metadata("design:returntype", Promise)
], FinancialController.prototype, "getCustomsDuty", null);
exports.FinancialController = FinancialController = FinancialController_1 = __decorate([
    (0, common_1.Controller)(),
    __metadata("design:paramtypes", [financial_service_1.FinancialService])
], FinancialController);
//# sourceMappingURL=financial.controller.js.map