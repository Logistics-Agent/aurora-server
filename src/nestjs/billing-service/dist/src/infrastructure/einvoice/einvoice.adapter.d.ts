export interface EInvoiceIssueInput {
    tenantId: string;
    invoiceId: string;
    invoiceNumber: string;
    totalAmount: number;
    customerId: string;
}
export interface EInvoiceIssueResult {
    taxAuthorityCode: string;
    eInvoiceUrl: string;
    signedAt: string;
    provider: string;
}
export interface EInvoiceAdapter {
    signAndIssue(input: EInvoiceIssueInput): Promise<EInvoiceIssueResult>;
}
export declare class VNPTEInvoiceAdapter implements EInvoiceAdapter {
    private readonly logger;
    signAndIssue(input: EInvoiceIssueInput): Promise<EInvoiceIssueResult>;
}
