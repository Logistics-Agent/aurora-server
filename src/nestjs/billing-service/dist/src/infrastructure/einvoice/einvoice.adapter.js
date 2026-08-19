"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var VNPTEInvoiceAdapter_1;
Object.defineProperty(exports, "__esModule", { value: true });
exports.VNPTEInvoiceAdapter = void 0;
const common_1 = require("@nestjs/common");
let VNPTEInvoiceAdapter = VNPTEInvoiceAdapter_1 = class VNPTEInvoiceAdapter {
    constructor() {
        this.logger = new common_1.Logger(VNPTEInvoiceAdapter_1.name);
    }
    async signAndIssue(input) {
        this.logger.log(`[e-Invoice Gateway - VNPT] Digitally signing invoice ${input.invoiceNumber} ($${input.totalAmount}) for Tenant ${input.tenantId}...`);
        const mockTaxCode = `TAX-VNPT-${Date.now().toString(36).toUpperCase()}`;
        const mockUrl = `https://einvoice.vnpt.vn/lookup?code=${mockTaxCode}&tenant=${input.tenantId}`;
        this.logger.log(`[e-Invoice Issued] Tax Authority Code: ${mockTaxCode} | URL: ${mockUrl}`);
        return {
            taxAuthorityCode: mockTaxCode,
            eInvoiceUrl: mockUrl,
            signedAt: new Date().toISOString(),
            provider: 'VNPT',
        };
    }
};
exports.VNPTEInvoiceAdapter = VNPTEInvoiceAdapter;
exports.VNPTEInvoiceAdapter = VNPTEInvoiceAdapter = VNPTEInvoiceAdapter_1 = __decorate([
    (0, common_1.Injectable)()
], VNPTEInvoiceAdapter);
//# sourceMappingURL=einvoice.adapter.js.map