import { Controller, UseFilters, UseInterceptors } from '@nestjs/common';
import { GrpcMethod } from '@nestjs/microservices';
import { BillingService } from '../../application/services/billing.service';
import { TenantInterceptor } from '../../common/interceptors/tenant.interceptor';
import { GrpcExceptionFilter } from '../../common/filters/grpc-exception.filter';
import {
  GenerateInvoiceRequest,
  CreateInvoiceRequest,
  InvoiceResponse,
  GetInvoiceRequest,
  InvoiceDetailResponse,
  ListInvoicesRequest,
  ListInvoicesResponse,
  UpdateInvoiceStatusRequest,
  CreditCheckRequest,
  CreditCheckResponse,
  CreateEscrowWalletRequest,
  WalletResponse,
  GetWalletBalanceRequest,
  FreezeEscrowRequest,
  ReleaseEscrowRequest,
  RefundEscrowRequest,
  TransactionResponse,
  RecordPaymentRequest,
  RecordPaymentResponse,
  CancelInvoiceRequest,
  IssueDebitNoteRequest,
  IssueCreditNoteRequest,
  AdjustmentNoteResponse,
} from '../dto/billing.dto';

@Controller()
@UseInterceptors(TenantInterceptor)
@UseFilters(GrpcExceptionFilter)
export class BillingController {
  constructor(private readonly billingService: BillingService) {}

  // ── Invoice gRPC Endpoints ────────────────────────────────────────────────

  @GrpcMethod('BillingService', 'GenerateInvoice')
  async generateInvoice(data: GenerateInvoiceRequest): Promise<InvoiceResponse> {
    return this.billingService.generateInvoice(data, data.tenantId);
  }

  @GrpcMethod('BillingService', 'GetInvoiceDetail')
  async getInvoiceDetail(data: GetInvoiceRequest): Promise<InvoiceDetailResponse> {
    return this.billingService.getInvoiceDetail(data);
  }

  @GrpcMethod('BillingService', 'CheckCustomerCredit')
  async checkCustomerCredit(data: CreditCheckRequest): Promise<CreditCheckResponse> {
    return this.billingService.checkCustomerCredit(data, data.tenantId);
  }

  @GrpcMethod('BillingService', 'CreateInvoice')
  async createInvoice(data: CreateInvoiceRequest): Promise<InvoiceResponse> {
    return this.billingService.createInvoice(data, data.tenantId);
  }

  @GrpcMethod('BillingService', 'GetInvoice')
  async getInvoice(data: GetInvoiceRequest): Promise<InvoiceResponse> {
    return this.billingService.getInvoice(data);
  }

  @GrpcMethod('BillingService', 'ListInvoices')
  async listInvoices(data: ListInvoicesRequest): Promise<ListInvoicesResponse> {
    return this.billingService.listInvoices(data, data.tenantId);
  }

  @GrpcMethod('BillingService', 'UpdateInvoiceStatus')
  async updateInvoiceStatus(data: UpdateInvoiceStatusRequest): Promise<InvoiceResponse> {
    return this.billingService.updateInvoiceStatus(data);
  }

  @GrpcMethod('BillingService', 'RecordPayment')
  async recordPayment(data: RecordPaymentRequest): Promise<RecordPaymentResponse> {
    return this.billingService.recordPayment(data, data.tenantId);
  }

  @GrpcMethod('BillingService', 'CancelInvoice')
  async cancelInvoice(data: CancelInvoiceRequest): Promise<InvoiceResponse> {
    return this.billingService.cancelInvoice(data);
  }

  // ── TASK-003: Debit / Credit Note Endpoints ───────────────────────────────

  @GrpcMethod('BillingService', 'IssueDebitNote')
  async issueDebitNote(data: IssueDebitNoteRequest): Promise<AdjustmentNoteResponse> {
    return this.billingService.issueDebitNote(data, data.tenantId);
  }

  @GrpcMethod('BillingService', 'IssueCreditNote')
  async issueCreditNote(data: IssueCreditNoteRequest): Promise<AdjustmentNoteResponse> {
    return this.billingService.issueCreditNote(data, data.tenantId);
  }

  // ── Escrow Wallet gRPC Endpoints ──────────────────────────────────────────

  @GrpcMethod('BillingService', 'CreateEscrowWallet')
  async createEscrowWallet(data: CreateEscrowWalletRequest): Promise<WalletResponse> {
    return this.billingService.createEscrowWallet(data, data.tenantId);
  }

  @GrpcMethod('BillingService', 'GetWalletBalance')
  async getWalletBalance(data: GetWalletBalanceRequest): Promise<WalletResponse> {
    return this.billingService.getWalletBalance(data);
  }

  @GrpcMethod('BillingService', 'FreezeEscrowAmount')
  async freezeEscrowAmount(data: FreezeEscrowRequest): Promise<TransactionResponse> {
    return this.billingService.freezeEscrowAmount(data);
  }

  @GrpcMethod('BillingService', 'ReleaseEscrowAmount')
  async releaseEscrowAmount(data: ReleaseEscrowRequest): Promise<TransactionResponse> {
    return this.billingService.releaseEscrowAmount(data);
  }

  @GrpcMethod('BillingService', 'RefundEscrowAmount')
  async refundEscrowAmount(data: RefundEscrowRequest): Promise<TransactionResponse> {
    return this.billingService.refundEscrowAmount(data);
  }
}
