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
var StorageService_1;
Object.defineProperty(exports, "__esModule", { value: true });
exports.StorageService = void 0;
const common_1 = require("@nestjs/common");
const config_1 = require("@nestjs/config");
let StorageService = StorageService_1 = class StorageService {
    constructor(configService) {
        this.configService = configService;
        this.logger = new common_1.Logger(StorageService_1.name);
        this.bucketName = this.configService.get('billing.s3BucketName', 'aurora-private-docs');
    }
    generateInvoiceS3Key(tenantId, invoiceId) {
        return `tenants/${tenantId}/billing/invoices/${invoiceId}.pdf`;
    }
    async renderAndUploadInvoicePdf(tenantId, invoiceId, invoiceNumber) {
        const s3Key = this.generateInvoiceS3Key(tenantId, invoiceId);
        this.logger.log(`Rendering PDF for invoice ${invoiceNumber} -> S3 Key: s3://${this.bucketName}/${s3Key}`);
        const presignedUrl = `https://r2.aurora.io/${this.bucketName}/${s3Key}?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Expires=86400`;
        return {
            s3Key,
            presignedUrl,
        };
    }
};
exports.StorageService = StorageService;
exports.StorageService = StorageService = StorageService_1 = __decorate([
    (0, common_1.Injectable)(),
    __metadata("design:paramtypes", [config_1.ConfigService])
], StorageService);
//# sourceMappingURL=storage.service.js.map