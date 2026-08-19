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
Object.defineProperty(exports, "__esModule", { value: true });
exports.BillingController = void 0;
const common_1 = require("@nestjs/common");
const microservices_1 = require("@nestjs/microservices");
const billing_service_1 = require("../../application/services/billing.service");
const tenant_interceptor_1 = require("../../common/interceptors/tenant.interceptor");
const grpc_exception_filter_1 = require("../../common/filters/grpc-exception.filter");
let BillingController = class BillingController {
    constructor(billingService) {
        this.billingService = billingService;
    }
    async generateInvoice(data) {
        return this.billingService.generateInvoice(data, data.tenantId);
    }
    async getInvoiceDetail(data) {
        return this.billingService.getInvoiceDetail(data);
    }
    async checkCustomerCredit(data) {
        return this.billingService.checkCustomerCredit(data, data.tenantId);
    }
    async createInvoice(data) {
        return this.billingService.createInvoice(data, data.tenantId);
    }
    async getInvoice(data) {
        return this.billingService.getInvoice(data);
    }
    async listInvoices(data) {
        return this.billingService.listInvoices(data, data.tenantId);
    }
    async updateInvoiceStatus(data) {
        return this.billingService.updateInvoiceStatus(data);
    }
    async recordPayment(data) {
        return this.billingService.recordPayment(data, data.tenantId);
    }
    async cancelInvoice(data) {
        return this.billingService.cancelInvoice(data);
    }
    async issueDebitNote(data) {
        return this.billingService.issueDebitNote(data, data.tenantId);
    }
    async issueCreditNote(data) {
        return this.billingService.issueCreditNote(data, data.tenantId);
    }
    async createEscrowWallet(data) {
        return this.billingService.createEscrowWallet(data, data.tenantId);
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
    (0, microservices_1.GrpcMethod)('BillingService', 'GenerateInvoice'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "generateInvoice", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'GetInvoiceDetail'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "getInvoiceDetail", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'CheckCustomerCredit'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "checkCustomerCredit", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'CreateInvoice'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object]),
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
    __metadata("design:paramtypes", [Object]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "listInvoices", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'UpdateInvoiceStatus'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "updateInvoiceStatus", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'RecordPayment'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "recordPayment", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'CancelInvoice'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "cancelInvoice", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'IssueDebitNote'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "issueDebitNote", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'IssueCreditNote'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object]),
    __metadata("design:returntype", Promise)
], BillingController.prototype, "issueCreditNote", null);
__decorate([
    (0, microservices_1.GrpcMethod)('BillingService', 'CreateEscrowWallet'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object]),
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
exports.BillingController = BillingController = __decorate([
    (0, common_1.Controller)(),
    (0, common_1.UseInterceptors)(tenant_interceptor_1.TenantInterceptor),
    (0, common_1.UseFilters)(grpc_exception_filter_1.GrpcExceptionFilter),
    __metadata("design:paramtypes", [billing_service_1.BillingService])
], BillingController);
//# sourceMappingURL=billing.controller.js.map