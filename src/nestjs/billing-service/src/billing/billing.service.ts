import { Injectable, Logger, NotFoundException, BadRequestException } from '@nestjs/common';
import { PrismaService } from '../prisma/prisma.service';
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

@Injectable()
export class BillingService {
  private readonly logger = new Logger(BillingService.name);

  constructor(private readonly prisma: PrismaService) {}

  // ── Invoice Operations ────────────────────────────────────────────────────

  async createInvoice(request: CreateInvoiceRequest): Promise<InvoiceResponse> {
    this.logger.log(`Creating invoice for shipment ${request.shipmentId} (Tenant: ${request.tenantId})`);

    const subtotal = request.items.reduce((sum, item) => sum + item.amount, 0);
    const taxAmount = subtotal * 0.05; // 5% VAT baseline
    const totalAmount = subtotal + taxAmount;

    const invoiceNumber = `INV-${new Date().getFullYear()}-${Math.floor(1000 + Math.random() * 9000)}`;
    const dueDate = request.dueDate ? new Date(request.dueDate) : new Date(Date.now() + 14 * 24 * 60 * 60 * 1000);

    const invoice = await this.prisma.invoice.create({
      data: {
        tenantId: request.tenantId,
        shipmentId: request.shipmentId,
        invoiceNumber: invoiceNumber,
        subtotal: Number(subtotal.toFixed(2)),
        taxAmount: Number(taxAmount.toFixed(2)),
        totalAmount: Number(totalAmount.toFixed(2)),
        status: 'UNPAID',
        dueDate: dueDate,
        items: {
          create: request.items.map((item) => ({
            description: item.description,
            amount: item.amount,
            category: item.category || 'FREIGHT',
          })),
        },
      },
      include: {
        items: true,
      },
    });

    return this.mapInvoiceResponse(invoice);
  }

  async getInvoice(request: GetInvoiceRequest): Promise<InvoiceResponse> {
    const invoice = await this.prisma.invoice.findUnique({
      where: { id: request.invoiceId },
      include: { items: true },
    });

    if (!invoice) {
      throw new NotFoundException(`Invoice with ID ${request.invoiceId} not found`);
    }

    return this.mapInvoiceResponse(invoice);
  }

  async listInvoices(request: ListInvoicesRequest): Promise<ListInvoicesResponse> {
    const page = request.page && request.page > 0 ? request.page : 1;
    const limit = request.limit && request.limit > 0 ? request.limit : 10;
    const skip = (page - 1) * limit;

    const where: any = { tenantId: request.tenantId };
    if (request.status) {
      where.status = request.status;
    }

    const [invoices, totalItems] = await Promise.all([
      this.prisma.invoice.findMany({
        where,
        skip,
        take: limit,
        orderBy: { createdAt: 'desc' },
        include: { items: true },
      }),
      this.prisma.invoice.count({ where }),
    ]);

    return {
      invoices: invoices.map((inv) => this.mapInvoiceResponse(inv)),
      totalItems,
      page,
      limit,
    };
  }

  async updateInvoiceStatus(request: UpdateInvoiceStatusRequest): Promise<InvoiceResponse> {
    const invoice = await this.prisma.invoice.update({
      where: { id: request.invoiceId },
      data: { status: request.status },
      include: { items: true },
    });

    return this.mapInvoiceResponse(invoice);
  }

  // ── Escrow Wallet Operations ──────────────────────────────────────────────

  async createEscrowWallet(request: CreateEscrowWalletRequest): Promise<WalletResponse> {
    this.logger.log(`Creating Escrow Wallet for Carrier ${request.carrierId} (Tenant: ${request.tenantId})`);

    const wallet = await this.prisma.escrowWallet.upsert({
      where: {
        tenantId_carrierId: {
          tenantId: request.tenantId,
          carrierId: request.carrierId,
        },
      },
      update: {},
      create: {
        tenantId: request.tenantId,
        carrierId: request.carrierId,
        balance: 5000.0, // initial balance for testing
        frozenAmount: 0.0,
        currency: request.currency || 'USD',
      },
    });

    return this.mapWalletResponse(wallet);
  }

  async getWalletBalance(request: GetWalletBalanceRequest): Promise<WalletResponse> {
    const wallet = await this.prisma.escrowWallet.findUnique({
      where: { id: request.walletId },
    });

    if (!wallet) {
      throw new NotFoundException(`Wallet with ID ${request.walletId} not found`);
    }

    return this.mapWalletResponse(wallet);
  }

  async freezeEscrowAmount(request: FreezeEscrowRequest): Promise<TransactionResponse> {
    this.logger.log(`Freezing $${request.amount} on wallet ${request.walletId} for shipment ${request.shipmentId}`);

    const wallet = await this.prisma.escrowWallet.findUnique({
      where: { id: request.walletId },
    });

    if (!wallet) {
      throw new NotFoundException(`Wallet with ID ${request.walletId} not found`);
    }

    const available = wallet.balance - wallet.frozenAmount;
    if (available < request.amount) {
      throw new BadRequestException(`Insufficient funds to freeze $${request.amount}. Available: $${available}`);
    }

    const [updatedWallet, transaction] = await this.prisma.$transaction([
      this.prisma.escrowWallet.update({
        where: { id: request.walletId },
        data: {
          frozenAmount: { increment: request.amount },
        },
      }),
      this.prisma.escrowTransaction.create({
        data: {
          walletId: request.walletId,
          shipmentId: request.shipmentId,
          type: 'FREEZE',
          amount: request.amount,
          status: 'SUCCESS',
          referenceNo: request.referenceNo || `FREEZE-${request.shipmentId}`,
        },
      }),
    ]);

    return this.mapTransactionResponse(transaction);
  }

  async releaseEscrowAmount(request: ReleaseEscrowRequest): Promise<TransactionResponse> {
    this.logger.log(`Releasing $${request.amount} on wallet ${request.walletId} for shipment ${request.shipmentId}`);

    const wallet = await this.prisma.escrowWallet.findUnique({
      where: { id: request.walletId },
    });

    if (!wallet) {
      throw new NotFoundException(`Wallet with ID ${request.walletId} not found`);
    }

    if (wallet.frozenAmount < request.amount) {
      throw new BadRequestException(`Cannot release $${request.amount}. Currently frozen: $${wallet.frozenAmount}`);
    }

    const [updatedWallet, transaction] = await this.prisma.$transaction([
      this.prisma.escrowWallet.update({
        where: { id: request.walletId },
        data: {
          balance: { decrement: request.amount },
          frozenAmount: { decrement: request.amount },
        },
      }),
      this.prisma.escrowTransaction.create({
        data: {
          walletId: request.walletId,
          shipmentId: request.shipmentId,
          type: 'RELEASE',
          amount: request.amount,
          status: 'SUCCESS',
          referenceNo: request.referenceNo || `RELEASE-${request.shipmentId}`,
        },
      }),
    ]);

    return this.mapTransactionResponse(transaction);
  }

  async refundEscrowAmount(request: RefundEscrowRequest): Promise<TransactionResponse> {
    this.logger.log(`Refunding $${request.amount} on wallet ${request.walletId} for shipment ${request.shipmentId}`);

    const wallet = await this.prisma.escrowWallet.findUnique({
      where: { id: request.walletId },
    });

    if (!wallet) {
      throw new NotFoundException(`Wallet with ID ${request.walletId} not found`);
    }

    if (wallet.frozenAmount < request.amount) {
      throw new BadRequestException(`Cannot refund $${request.amount}. Currently frozen: $${wallet.frozenAmount}`);
    }

    const [updatedWallet, transaction] = await this.prisma.$transaction([
      this.prisma.escrowWallet.update({
        where: { id: request.walletId },
        data: {
          frozenAmount: { decrement: request.amount },
        },
      }),
      this.prisma.escrowTransaction.create({
        data: {
          walletId: request.walletId,
          shipmentId: request.shipmentId,
          type: 'REFUND',
          amount: request.amount,
          status: 'SUCCESS',
          referenceNo: request.referenceNo || `REFUND-${request.shipmentId}`,
        },
      }),
    ]);

    return this.mapTransactionResponse(transaction);
  }

  // ── Helper Mappers ────────────────────────────────────────────────────────

  private mapInvoiceResponse(invoice: any): InvoiceResponse {
    return {
      id: invoice.id,
      tenantId: invoice.tenantId,
      shipmentId: invoice.shipmentId,
      invoiceNumber: invoice.invoiceNumber,
      subtotal: invoice.subtotal,
      taxAmount: invoice.taxAmount,
      totalAmount: invoice.totalAmount,
      status: invoice.status,
      dueDate: invoice.dueDate ? invoice.dueDate.toISOString() : '',
      createdAt: invoice.createdAt ? invoice.createdAt.toISOString() : '',
      items: (invoice.items || []).map((item: any) => ({
        id: item.id,
        description: item.description,
        amount: item.amount,
        category: item.category,
      })),
    };
  }

  private mapWalletResponse(wallet: any): WalletResponse {
    return {
      walletId: wallet.id,
      tenantId: wallet.tenantId,
      carrierId: wallet.carrierId,
      balance: wallet.balance,
      frozenAmount: wallet.frozenAmount,
      availableAmount: Number((wallet.balance - wallet.frozenAmount).toFixed(2)),
      currency: wallet.currency,
    };
  }

  private mapTransactionResponse(tx: any): TransactionResponse {
    return {
      transactionId: tx.id,
      walletId: tx.walletId,
      shipmentId: tx.shipmentId,
      type: tx.type,
      amount: tx.amount,
      status: tx.status,
      referenceNo: tx.referenceNo || '',
      createdAt: tx.createdAt ? tx.createdAt.toISOString() : '',
    };
  }
}
