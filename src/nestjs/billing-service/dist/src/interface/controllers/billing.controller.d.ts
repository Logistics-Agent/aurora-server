import { BillingService } from '../../application/services/billing.service';
import { GenerateInvoiceRequest, CreateInvoiceRequest, InvoiceResponse, GetInvoiceRequest, InvoiceDetailResponse, ListInvoicesRequest, ListInvoicesResponse, UpdateInvoiceStatusRequest, CreditCheckRequest, CreditCheckResponse, CreateEscrowWalletRequest, WalletResponse, GetWalletBalanceRequest, FreezeEscrowRequest, ReleaseEscrowRequest, RefundEscrowRequest, TransactionResponse, RecordPaymentRequest, RecordPaymentResponse, CancelInvoiceRequest, IssueDebitNoteRequest, IssueCreditNoteRequest, AdjustmentNoteResponse } from '../dto/billing.dto';
export declare class BillingController {
    private readonly billingService;
    constructor(billingService: BillingService);
    generateInvoice(data: GenerateInvoiceRequest): Promise<InvoiceResponse>;
    getInvoiceDetail(data: GetInvoiceRequest): Promise<InvoiceDetailResponse>;
    checkCustomerCredit(data: CreditCheckRequest): Promise<CreditCheckResponse>;
    createInvoice(data: CreateInvoiceRequest): Promise<InvoiceResponse>;
    getInvoice(data: GetInvoiceRequest): Promise<InvoiceResponse>;
    listInvoices(data: ListInvoicesRequest): Promise<ListInvoicesResponse>;
    updateInvoiceStatus(data: UpdateInvoiceStatusRequest): Promise<InvoiceResponse>;
    recordPayment(data: RecordPaymentRequest): Promise<RecordPaymentResponse>;
    cancelInvoice(data: CancelInvoiceRequest): Promise<InvoiceResponse>;
    issueDebitNote(data: IssueDebitNoteRequest): Promise<AdjustmentNoteResponse>;
    issueCreditNote(data: IssueCreditNoteRequest): Promise<AdjustmentNoteResponse>;
    createEscrowWallet(data: CreateEscrowWalletRequest): Promise<WalletResponse>;
    getWalletBalance(data: GetWalletBalanceRequest): Promise<WalletResponse>;
    freezeEscrowAmount(data: FreezeEscrowRequest): Promise<TransactionResponse>;
    releaseEscrowAmount(data: ReleaseEscrowRequest): Promise<TransactionResponse>;
    refundEscrowAmount(data: RefundEscrowRequest): Promise<TransactionResponse>;
}
