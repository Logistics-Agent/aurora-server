import { Controller, Logger } from '@nestjs/common';
import { GrpcMethod } from '@nestjs/microservices';
import { Metadata } from '@grpc/grpc-js';
import { BillingService } from './billing.service';
import {
  CreateInvoiceRequest,
  InvoiceResponse,
  GetInvoiceRequest,
  ListInvoicesRequest,
  ListInvoicesResponse,
  UpdateInvoiceStatusRequest,
  CreateEscrowWalletRequest,
  WalletResponse,
  GetWalletBalanceRequest,
  FreezeEscrowRequest,
  ReleaseEscrowRequest,
  RefundEscrowRequest,
  TransactionResponse,
} from './dto/billing.dto';

@Controller()
export class BillingController {
  private readonly logger = new Logger(BillingController.name);

  constructor(private readonly billingService: BillingService) {}

  // ── Invoice gRPC Endpoints ────────────────────────────────────────────────

  @GrpcMethod('BillingService', 'CreateInvoice')
  async createInvoice(data: CreateInvoiceRequest, metadata: Metadata): Promise<InvoiceResponse> {
    const tenantIdHeader = metadata.get('x-tenant-id');
    const tenantId = tenantIdHeader.length > 0 ? String(tenantIdHeader[0]) : data.tenantId;

    return this.billingService.createInvoice({
      ...data,
      tenantId: tenantId || data.tenantId,
    });
  }

  @GrpcMethod('BillingService', 'GetInvoice')
  async getInvoice(data: GetInvoiceRequest): Promise<InvoiceResponse> {
    return this.billingService.getInvoice(data);
  }

  @GrpcMethod('BillingService', 'ListInvoices')
  async listInvoices(data: ListInvoicesRequest, metadata: Metadata): Promise<ListInvoicesResponse> {
    const tenantIdHeader = metadata.get('x-tenant-id');
    const tenantId = tenantIdHeader.length > 0 ? String(tenantIdHeader[0]) : data.tenantId;

    return this.billingService.listInvoices({
      ...data,
      tenantId: tenantId || data.tenantId,
    });
  }

  @GrpcMethod('BillingService', 'UpdateInvoiceStatus')
  async updateInvoiceStatus(data: UpdateInvoiceStatusRequest): Promise<InvoiceResponse> {
    return this.billingService.updateInvoiceStatus(data);
  }

  // ── Escrow Wallet gRPC Endpoints ──────────────────────────────────────────

  @GrpcMethod('BillingService', 'CreateEscrowWallet')
  async createEscrowWallet(data: CreateEscrowWalletRequest, metadata: Metadata): Promise<WalletResponse> {
    const tenantIdHeader = metadata.get('x-tenant-id');
    const tenantId = tenantIdHeader.length > 0 ? String(tenantIdHeader[0]) : data.tenantId;

    return this.billingService.createEscrowWallet({
      ...data,
      tenantId: tenantId || data.tenantId,
    });
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
