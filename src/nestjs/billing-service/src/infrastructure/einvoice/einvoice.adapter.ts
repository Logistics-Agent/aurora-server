import { Injectable, Logger } from '@nestjs/common';

export interface EInvoiceIssueInput {
  tenantId: string;
  invoiceId: string;
  invoiceNumber: string;
  totalAmount: number;
  customerId: string;
}

export interface EInvoiceIssueResult {
  taxAuthorityCode: string; // Mã của Cơ quan Thuế
  eInvoiceUrl: string;       // Link tra cứu hóa đơn điện tử
  signedAt: string;
  provider: string;          // VNPT | VIETTEL | MISA
}

export interface EInvoiceAdapter {
  signAndIssue(input: EInvoiceIssueInput): Promise<EInvoiceIssueResult>;
}

@Injectable()
export class VNPTEInvoiceAdapter implements EInvoiceAdapter {
  private readonly logger = new Logger(VNPTEInvoiceAdapter.name);

  async signAndIssue(input: EInvoiceIssueInput): Promise<EInvoiceIssueResult> {
    this.logger.log(
      `[e-Invoice Gateway - VNPT] Digitally signing invoice ${input.invoiceNumber} ($${input.totalAmount}) for Tenant ${input.tenantId}...`,
    );

    // Phase 1: Mock digital signature & tax code generation
    // Phase 2: Call VNPT E-Invoice REST API / SOAP Service
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
}
