import { PrismaService } from '../prisma/prisma.service';
import { CreateInvoiceRequest, InvoiceResponse, GetInvoiceRequest, ListInvoicesRequest, ListInvoicesResponse, UpdateInvoiceStatusRequest, CreateEscrowWalletRequest, WalletResponse, GetWalletBalanceRequest, FreezeEscrowRequest, ReleaseEscrowRequest, RefundEscrowRequest, TransactionResponse } from './dto/billing.dto';
export declare class BillingService {
    private readonly prisma;
    private readonly logger;
    constructor(prisma: PrismaService);
    createInvoice(request: CreateInvoiceRequest): Promise<InvoiceResponse>;
    getInvoice(request: GetInvoiceRequest): Promise<InvoiceResponse>;
    listInvoices(request: ListInvoicesRequest): Promise<ListInvoicesResponse>;
    updateInvoiceStatus(request: UpdateInvoiceStatusRequest): Promise<InvoiceResponse>;
    createEscrowWallet(request: CreateEscrowWalletRequest): Promise<WalletResponse>;
    getWalletBalance(request: GetWalletBalanceRequest): Promise<WalletResponse>;
    freezeEscrowAmount(request: FreezeEscrowRequest): Promise<TransactionResponse>;
    releaseEscrowAmount(request: ReleaseEscrowRequest): Promise<TransactionResponse>;
    refundEscrowAmount(request: RefundEscrowRequest): Promise<TransactionResponse>;
    private mapInvoiceResponse;
    private mapWalletResponse;
    private mapTransactionResponse;
}
