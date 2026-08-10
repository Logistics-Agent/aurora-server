export declare class InvoiceDomainService {
    generateInvoiceNumber(seqNumber: number, date?: Date): string;
    calculateDueDate(startDate?: Date, termsDays?: number): Date;
    calculateInvoiceTotals(items: {
        quantity: number;
        unitPrice: number;
    }[], taxRatePercent?: number): {
        subtotal: number;
        taxAmount: number;
        totalAmount: number;
    };
    evaluateCreditApproval(creditLimit: number, currentDebt: number, overdueCount: number, requestedAmount: number): {
        isApproved: boolean;
        availableCredit: number;
        reason: string;
    };
}
