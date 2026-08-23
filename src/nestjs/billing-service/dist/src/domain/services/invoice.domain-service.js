"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.InvoiceDomainService = void 0;
const common_1 = require("@nestjs/common");
let InvoiceDomainService = class InvoiceDomainService {
    generateInvoiceNumber(seqNumber, date = new Date()) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const seqFormatted = String(seqNumber).padStart(4, '0');
        return `INV-${year}${month}-${seqFormatted}`;
    }
    calculateDueDate(startDate = new Date(), termsDays = 30) {
        const dueDate = new Date(startDate.getTime());
        dueDate.setDate(dueDate.getDate() + (termsDays || 30));
        return dueDate;
    }
    calculateInvoiceTotals(items, taxRatePercent = 5.0) {
        const subtotal = items.reduce((sum, item) => sum + (item.quantity || 1) * (item.unitPrice || 0), 0);
        const taxAmount = subtotal * ((taxRatePercent || 0) / 100.0);
        const totalAmount = subtotal + taxAmount;
        return {
            subtotal: Number(subtotal.toFixed(2)),
            taxAmount: Number(taxAmount.toFixed(2)),
            totalAmount: Number(totalAmount.toFixed(2)),
        };
    }
    evaluateCreditApproval(creditLimit, currentDebt, overdueCount, requestedAmount) {
        const availableCredit = Math.max(0, creditLimit - currentDebt);
        if (overdueCount > 0) {
            return {
                isApproved: false,
                availableCredit,
                reason: `Customer has ${overdueCount} overdue unpaid invoices. Credit blocked.`,
            };
        }
        if (requestedAmount > availableCredit) {
            return {
                isApproved: false,
                availableCredit,
                reason: `Requested amount $${requestedAmount} exceeds available credit $${availableCredit}.`,
            };
        }
        return {
            isApproved: true,
            availableCredit: Number((availableCredit - requestedAmount).toFixed(2)),
            reason: 'Credit check approved.',
        };
    }
};
exports.InvoiceDomainService = InvoiceDomainService;
exports.InvoiceDomainService = InvoiceDomainService = __decorate([
    (0, common_1.Injectable)()
], InvoiceDomainService);
//# sourceMappingURL=invoice.domain-service.js.map