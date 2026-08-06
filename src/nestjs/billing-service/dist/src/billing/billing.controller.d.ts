import { Metadata } from '@grpc/grpc-js';
import { BillingService } from './billing.service';
import { CreateInvoiceRequest, InvoiceResponse, GetInvoiceRequest, ListInvoicesRequest, ListInvoicesResponse, UpdateInvoiceStatusRequest, CreateEscrowWalletRequest, WalletResponse, GetWalletBalanceRequest, FreezeEscrowRequest, ReleaseEscrowRequest, RefundEscrowRequest, TransactionResponse } from './dto/billing.dto';
export declare class BillingController {
    private readonly billingService;
    private readonly logger;
    constructor(billingService: BillingService);
    createInvoice(data: CreateInvoiceRequest, metadata: Metadata): Promise<InvoiceResponse>;
    getInvoice(data: GetInvoiceRequest): Promise<InvoiceResponse>;
    listInvoices(data: ListInvoicesRequest, metadata: Metadata): Promise<ListInvoicesResponse>;
    updateInvoiceStatus(data: UpdateInvoiceStatusRequest): Promise<InvoiceResponse>;
    createEscrowWallet(data: CreateEscrowWalletRequest, metadata: Metadata): Promise<WalletResponse>;
    getWalletBalance(data: GetWalletBalanceRequest): Promise<WalletResponse>;
    freezeEscrowAmount(data: FreezeEscrowRequest): Promise<TransactionResponse>;
    releaseEscrowAmount(data: ReleaseEscrowRequest): Promise<TransactionResponse>;
    refundEscrowAmount(data: RefundEscrowRequest): Promise<TransactionResponse>;
}
