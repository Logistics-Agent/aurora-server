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
var BillingController_1;
Object.defineProperty(exports, "__esModule", { value: true });
exports.BillingController = void 0;
const common_1 = require("@nestjs/common");
const microservices_1 = require("@nestjs/microservices");
const grpc_js_1 = require("@grpc/grpc-js");
const billing_service_1 = require("./billing.service");
let BillingController = BillingController_1 = class BillingController {
    constructor(billingService) {
        this.billingService = billingService;
        this.logger = new common_1.Logger(BillingController_1.name);
    }
    async createInvoice(data, metadata) {
        const tenantIdHeader = metadata.get('x-tenant-id');
        const tenantId = tenantIdHeader.length > 0 ? String(tenantIdHeader[0]) : data.tenantId;
        return this.billingService.createInvoice({
            ...data,
            tenantId: tenantId || data.tenantId,
        });
    }
    async getInvoice(data) {
        return this.billingService.getInvoice(data);
    }
    async listInvoices(data, metadata) {
        const tenantIdHeader = metadata.get('x-tenant-id');
        const tenantId = tenantIdHeader.length > 0 ? String(tenantIdHeader[0]) : data.tenantId;
        return this.billingService.listInvoices({
            ...data,
            tenantId: tenantId || data.tenantId,
        });
    }
    async updateInvoiceStatus(data) {
        return this.billingService.updateInvoiceStatus(data);
    }
    async createEscrowWallet(data, metadata) {
        const tenantIdHeader = metadata.get('x-tenant-id');
        const tenantId = tenantIdHeader.length > 0 ? String(tenantIdHeader[0]) : data.tenantId;
        return this.billingService.createEscrowWallet({
            ...data,
            tenantId: tenantId || data.tenantId,
        });
    }
    async getWalletBalance(data) {
        return this.billingService.getWalletBalance(data);
    }
    async freezeEscrowAmount(data) {
        return this.billingService.freezeEscrowAmount(data);
    }
    async releaseEscrowAmount(data) {
        return this.billingService.releaseEscrowAmount(data);
    }
    async refundEscrowAmount(data) {
        return this.billingService.refundEscrowAmount(data);
    }
};
exports.BillingController = BillingController;
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'CreateInvoice'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object, grpc_js_1.Metadata]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "createInvoice", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'GetInvoice'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "getInvoice", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'ListInvoices'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object, grpc_js_1.Metadata]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "listInvoices", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'UpdateInvoiceStatus'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "updateInvoiceStatus", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'CreateEscrowWallet'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object, grpc_js_1.Metadata]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "createEscrowWallet", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'GetWalletBalance'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "getWalletBalance", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'FreezeEscrowAmount'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "freezeEscrowAmount", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'ReleaseEscrowAmount'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "releaseEscrowAmount", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'RefundEscrowAmount'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "refundEscrowAmount", null);
exports.BillingController = BillingController = BillingController_1 = __decorate([
    (0, common_1.Controller)(),
    __metadata("design:paramtypes", [billing_service_1.BillingService])
], BillingController);
//# sourceMappingURL=billing.controller.js.map