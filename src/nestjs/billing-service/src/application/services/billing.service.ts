import { Injectable, Logger, NotFoundException, BadRequestException } from '@nestjs/common';
import { PrismaService } from '../../infrastructure/prisma/prisma.service';
import { InvoiceDomainService } from '../../domain/services/invoice.domain-service';
import { GenerateInvoiceUseCase } from '../use-cases/generate-invoice.use-case';
import { RabbitMQMessagingService } from '../../infrastructure/messaging/rabbitmq.service';
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
} from '../../interface/dto/billing.dto';

@Injectable()
export class BillingService {
  private readonly logger = new Logger(BillingService.name);

  constructor(
    private readonly prisma: PrismaService,
    private readonly domainService: InvoiceDomainService,
    private readonly generateInvoiceUseCase: GenerateInvoiceUseCase,
    private readonly messagingService: RabbitMQMessagingService,
  ) {}

  // ── Invoice Operations ────────────────────────────────────────────────────

  async generateInvoice(
    request: GenerateInvoiceRequest,
    tenantId?: string,
  ): Promise<InvoiceResponse> {
    const effectiveTenantId = tenantId || request.tenantId || 'a0000000-0000-0000-0000-000000000001';

    const invoice = await this.generateInvoiceUseCase.execute({
      tenantId: effectiveTenantId,
      shipmentId: request.shipmentId,
      customerId: request.customerId,
      paymentTermsDays: request.paymentTermsDays,
    });

    return this.mapInvoiceResponse(invoice);
  }

  async getInvoiceDetail(request: GetInvoiceRequest): Promise<InvoiceDetailResponse> {
    const invoice = await this.prisma.invoice.findUnique({
      where: { id: request.invoiceId },
      include: {
        items: true,
        payments: true,
      },
    });

    if (!invoice) {
      throw new NotFoundException(`Invoice with ID ${request.invoiceId} not found`);
    }

    const totalPaid = invoice.payments.reduce((sum, p) => sum + p.amountPaid, 0);
    const remainingBalance = Number(Math.max(0, invoice.totalAmount - totalPaid).toFixed(2));

    return {
      invoice: this.mapInvoiceResponse(invoice),
      payments: invoice.payments.map((p) => ({
        id: p.id,
        amountPaid: p.amountPaid,
        paymentMethod: p.paymentMethod,
        transactionRef: p.transactionRef || '',
        status: p.status,
        createdAt: p.createdAt.toISOString(),
      })),
      remainingBalance,
    };
  }

  async checkCustomerCredit(
    request: CreditCheckRequest,
    tenantId?: string,
  ): Promise<CreditCheckResponse> {
    const effectiveTenantId = tenantId || request.tenantId || 'a0000000-0000-0000-0000-000000000001';
    const customerId = request.customerId || 'CUST-001';
    const creditLimit = 50000.0; // Mock customer credit limit $50,000

    const unpaidInvoices = await this.prisma.invoice.findMany({
      where: {
        tenantId: effectiveTenantId,
        customerId: customerId,
        status: { in: ['UNPAID', 'PARTIALLY_PAID', 'OVERDUE'] },
      },
      include: { payments: true },
    });

    const now = new Date();
    let currentOutstandingDebt = 0;
    let overdueInvoiceCount = 0;

    for (const inv of unpaidInvoices) {
      const paid = inv.payments.reduce((sum, p) => sum + p.amountPaid, 0);
      const debt = inv.totalAmount - paid;
      currentOutstandingDebt += debt;

      if (inv.dueDate < now || inv.status === 'OVERDUE') {
        overdueInvoiceCount += 1;
      }
    }

    const evaluation = this.domainService.evaluateCreditApproval(
      creditLimit,
      currentOutstandingDebt,
      overdueInvoiceCount,
      request.newAmount || 0,
    );

    return {
      customerId,
      isCreditApproved: evaluation.isApproved,
      creditLimit,
      currentOutstandingDebt: Number(currentOutstandingDebt.toFixed(2)),
      availableCredit: evaluation.availableCredit,
      overdueInvoiceCount,
      message: evaluation.reason,
    };
  }

  async createInvoice(
    request: CreateInvoiceRequest,
    tenantId?: string,
  ): Promise<InvoiceResponse> {
    const effectiveTenantId = tenantId || request.tenantId || 'a0000000-0000-0000-0000-000000000001';

    const items = request.items.map((i) => ({
      quantity: i.quantity || 1,
      unitPrice: i.unitPrice || i.amount,
      amount: i.amount,
      description: i.description,
      category: i.category || 'FREIGHT',
    }));

    const totals = this.domainService.calculateInvoiceTotals(items, 5.0);

    const invoiceCount = await this.prisma.invoice.count({
      where: { tenantId: effectiveTenantId },
    });
    const invoiceNumber = this.domainService.generateInvoiceNumber(invoiceCount + 1);
    const dueDate = request.dueDate
      ? new Date(request.dueDate)
      : this.domainService.calculateDueDate(new Date(), 30);

    const invoice = await this.prisma.invoice.create({
      data: {
        tenantId: effectiveTenantId,
        shipmentId: request.shipmentId,
        customerId: request.customerId || 'CUST-001',
        invoiceNumber: invoiceNumber,
        subtotal: totals.subtotal,
        taxAmount: totals.taxAmount,
        totalAmount: totals.totalAmount,
        status: 'UNPAID',
        dueDate: dueDate,
        items: {
          create: items,
        },
      },
      include: { items: true },
    });

    return this.mapInvoiceResponse(invoice);
  }

  async getInvoice(request: GetInvoiceRequest): Promise<InvoiceResponse> {
    const detail = await this.getInvoiceDetail(request);
    return detail.invoice;
  }

  async listInvoices(
    request: ListInvoicesRequest,
    tenantId?: string,
  ): Promise<ListInvoicesResponse> {
    const effectiveTenantId = tenantId || request.tenantId || 'a0000000-0000-0000-0000-000000000001';
    const page = request.page && request.page > 0 ? request.page : 1;
    const limit = request.limit && request.limit > 0 ? request.limit : 10;
    const skip = (page - 1) * limit;

    const where: any = { tenantId: effectiveTenantId };
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
    const existing = await this.prisma.invoice.findUnique({
      where: { id: request.invoiceId },
      include: { payments: true },
    });

    if (!existing) {
      throw new NotFoundException(`Invoice ${request.invoiceId} not found`);
    }

    // Business Rule: Cannot manually set PAID unless payments actually cover the total
    if (request.status === 'PAID') {
      const totalPaid = existing.payments.reduce((sum, p) => sum + p.amountPaid, 0);
      if (totalPaid < existing.totalAmount) {
        throw new BadRequestException(
          `Cannot mark invoice as PAID. Total paid ($${totalPaid.toFixed(2)}) is less than invoice total ($${existing.totalAmount.toFixed(2)}). Use RecordPayment instead.`,
        );
      }
    }

    // Business Rule: Cannot modify a CANCELLED invoice
    if (existing.status === 'CANCELLED') {
      throw new BadRequestException(`Invoice ${existing.invoiceNumber} is CANCELLED and cannot be modified.`);
    }

    const invoice = await this.prisma.invoice.update({
      where: { id: request.invoiceId },
      data: { status: request.status },
      include: { items: true },
    });

    return this.mapInvoiceResponse(invoice);
  }

  // ── RecordPayment: Ghi nhận thanh toán + tự động cập nhật trạng thái PAID ──

  async recordPayment(
    request: RecordPaymentRequest,
    tenantId?: string,
  ): Promise<RecordPaymentResponse> {
    const effectiveTenantId = tenantId || request.tenantId || 'a0000000-0000-0000-0000-000000000001';

    const invoice = await this.prisma.invoice.findUnique({
      where: { id: request.invoiceId },
      include: { payments: true },
    });

    if (!invoice) {
      throw new NotFoundException(`Invoice ${request.invoiceId} not found`);
    }

    if (invoice.status === 'CANCELLED') {
      throw new BadRequestException(`Cannot record payment for a CANCELLED invoice.`);
    }

    if (invoice.status === 'PAID') {
      throw new BadRequestException(`Invoice ${invoice.invoiceNumber} is already fully PAID.`);
    }

    if (request.amountPaid <= 0) {
      throw new BadRequestException(`Payment amount must be greater than 0.`);
    }

    // ── ACID Transaction: create payment record + auto-update invoice status ─
    const totalAlreadyPaid = invoice.payments.reduce((sum, p) => sum + p.amountPaid, 0);
    const newTotalPaid = totalAlreadyPaid + request.amountPaid;
    const remainingBalance = Number(Math.max(0, invoice.totalAmount - newTotalPaid).toFixed(2));

    // Determine new invoice status based on payment
    let newStatus: string;
    if (newTotalPaid >= invoice.totalAmount) {
      newStatus = 'PAID';
    } else if (newTotalPaid > 0) {
      newStatus = 'PARTIALLY_PAID';
    } else {
      newStatus = invoice.status;
    }

    const [paymentRecord] = await this.prisma.$transaction([
      this.prisma.paymentRecord.create({
        data: {
          tenantId: effectiveTenantId,
          invoiceId: request.invoiceId,
          amountPaid: request.amountPaid,
          paymentMethod: request.paymentMethod || 'BANK_TRANSFER',
          transactionRef: request.transactionRef || `PAY-${Date.now()}`,
          status: 'SUCCESS',
        },
      }),
      this.prisma.invoice.update({
        where: { id: request.invoiceId },
        data: { status: newStatus },
      }),
    ]);

    // Publish payment.received event to RabbitMQ
    await this.messagingService.publishPaymentReceived({
      tenantId: effectiveTenantId,
      invoiceId: request.invoiceId,
      paymentRecordId: paymentRecord.id,
      amountPaid: request.amountPaid,
      paymentMethod: paymentRecord.paymentMethod,
      transactionRef: paymentRecord.transactionRef || '',
      newInvoiceStatus: newStatus,
      createdAt: paymentRecord.createdAt.toISOString(),
    });

    this.logger.log(
      `Payment recorded for Invoice ${invoice.invoiceNumber}: $${request.amountPaid} | New Status: ${newStatus} | Remaining: $${remainingBalance}`,
    );

    return {
      paymentRecordId: paymentRecord.id,
      invoiceId: request.invoiceId,
      amountPaid: paymentRecord.amountPaid,
      paymentMethod: paymentRecord.paymentMethod,
      transactionRef: paymentRecord.transactionRef || '',
      newInvoiceStatus: newStatus,
      remainingBalance,
      createdAt: paymentRecord.createdAt.toISOString(),
    };
  }

  // ── CancelInvoice: Hủy Hóa đơn ──────────────────────────────────────────

  async cancelInvoice(request: CancelInvoiceRequest): Promise<InvoiceResponse> {
    const invoice = await this.prisma.invoice.findUnique({
      where: { id: request.invoiceId },
      include: { items: true },
    });

    if (!invoice) {
      throw new NotFoundException(`Invoice ${request.invoiceId} not found`);
    }

    // Business Rule: Only UNPAID invoices can be cancelled
    if (invoice.status === 'PAID' || invoice.status === 'PARTIALLY_PAID') {
      throw new BadRequestException(
        `Cannot cancel invoice ${invoice.invoiceNumber} with status '${invoice.status}'. Only UNPAID invoices can be cancelled.`,
      );
    }

    if (invoice.status === 'CANCELLED') {
      throw new BadRequestException(`Invoice ${invoice.invoiceNumber} is already CANCELLED.`);
    }

    const cancelled = await this.prisma.invoice.update({
      where: { id: request.invoiceId },
      data: { status: 'CANCELLED' },
      include: { items: true },
    });

    this.logger.log(
      `Invoice ${cancelled.invoiceNumber} cancelled. Reason: ${request.reason || 'No reason provided'}`,
    );

    return this.mapInvoiceResponse(cancelled);
  }

  // ── TASK-003: Debit Note & Credit Note Operations ─────────────────────────

  async issueDebitNote(
    request: IssueDebitNoteRequest,
    tenantId?: string,
  ): Promise<AdjustmentNoteResponse> {
    const effectiveTenantId = tenantId || request.tenantId || 'a0000000-0000-0000-0000-000000000001';

    const invoice = await this.prisma.invoice.findUnique({
      where: { id: request.invoiceId },
    });

    if (!invoice) {
      throw new NotFoundException(`Invoice ${request.invoiceId} not found`);
    }

    if (invoice.status === 'CANCELLED') {
      throw new BadRequestException(`Cannot issue Debit Note for a CANCELLED invoice.`);
    }

    if (request.amount <= 0) {
      throw new BadRequestException(`Debit Note amount must be greater than 0.`);
    }

    const newTotalAmount = Number((invoice.totalAmount + request.amount).toFixed(2));

    const [note, updatedInvoice] = await this.prisma.$transaction([
      this.prisma.adjustmentNote.create({
        data: {
          tenantId: effectiveTenantId,
          invoiceId: request.invoiceId,
          type: 'DEBIT',
          reasonCode: request.reasonCode || 'DEMURRAGE',
          amount: request.amount,
          description: request.description || 'Extra charge incurred',
          status: 'ISSUED',
        },
      }),
      this.prisma.invoice.update({
        where: { id: request.invoiceId },
        data: { totalAmount: newTotalAmount },
      }),
    ]);

    this.logger.log(
      `Issued Debit Note ${note.id} for Invoice ${invoice.invoiceNumber}: +$${request.amount} | Reason: ${request.reasonCode} | New Total: $${newTotalAmount}`,
    );

    return {
      adjustmentNoteId: note.id,
      invoiceId: request.invoiceId,
      type: note.type,
      reasonCode: note.reasonCode,
      amount: note.amount,
      description: note.description,
      status: note.status,
      newInvoiceTotalAmount: updatedInvoice.totalAmount,
      createdAt: note.createdAt.toISOString(),
    };
  }

  async issueCreditNote(
    request: IssueCreditNoteRequest,
    tenantId?: string,
  ): Promise<AdjustmentNoteResponse> {
    const effectiveTenantId = tenantId || request.tenantId || 'a0000000-0000-0000-0000-000000000001';

    const invoice = await this.prisma.invoice.findUnique({
      where: { id: request.invoiceId },
    });

    if (!invoice) {
      throw new NotFoundException(`Invoice ${request.invoiceId} not found`);
    }

    if (invoice.status === 'CANCELLED') {
      throw new BadRequestException(`Cannot issue Credit Note for a CANCELLED invoice.`);
    }

    if (request.amount <= 0) {
      throw new BadRequestException(`Credit Note amount must be greater than 0.`);
    }

    const newTotalAmount = Number(Math.max(0, invoice.totalAmount - request.amount).toFixed(2));

    const [note, updatedInvoice] = await this.prisma.$transaction([
      this.prisma.adjustmentNote.create({
        data: {
          tenantId: effectiveTenantId,
          invoiceId: request.invoiceId,
          type: 'CREDIT',
          reasonCode: request.reasonCode || 'OVERCHARGE',
          amount: request.amount,
          description: request.description || 'Discount or refund applied',
          status: 'ISSUED',
        },
      }),
      this.prisma.invoice.update({
        where: { id: request.invoiceId },
        data: { totalAmount: newTotalAmount },
      }),
    ]);

    this.logger.log(
      `Issued Credit Note ${note.id} for Invoice ${invoice.invoiceNumber}: -$${request.amount} | Reason: ${request.reasonCode} | New Total: $${newTotalAmount}`,
    );

    return {
      adjustmentNoteId: note.id,
      invoiceId: request.invoiceId,
      type: note.type,
      reasonCode: note.reasonCode,
      amount: note.amount,
      description: note.description,
      status: note.status,
      newInvoiceTotalAmount: updatedInvoice.totalAmount,
      createdAt: note.createdAt.toISOString(),
    };
  }


  // ── Escrow Wallet Operations ──────────────────────────────────────────────

  async createEscrowWallet(
    request: CreateEscrowWalletRequest,
    tenantId?: string,
  ): Promise<WalletResponse> {
    const effectiveTenantId = tenantId || request.tenantId || 'a0000000-0000-0000-0000-000000000001';

    const wallet = await this.prisma.escrowWallet.upsert({
      where: {
        tenantId_carrierId: {
          tenantId: effectiveTenantId,
          carrierId: request.carrierId,
        },
      },
      update: {},
      create: {
        tenantId: effectiveTenantId,
        carrierId: request.carrierId,
        balance: 10000.0,
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
    const wallet = await this.prisma.escrowWallet.findUnique({
      where: { id: request.walletId },
    });

    if (!wallet) {
      throw new NotFoundException(`Wallet with ID ${request.walletId} not found`);
    }

    const available = wallet.balance - wallet.frozenAmount;
    if (available < request.amount) {
      throw new BadRequestException(`Insufficient available funds to freeze $${request.amount}. Available: $${available}`);
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
      customerId: invoice.customerId || 'CUST-001',
      invoiceNumber: invoice.invoiceNumber,
      subtotal: invoice.subtotal,
      taxAmount: invoice.taxAmount,
      totalAmount: invoice.totalAmount,
      currency: invoice.currency || 'USD',
      status: invoice.status,
      dueDate: invoice.dueDate ? invoice.dueDate.toISOString() : '',
      pdfS3Key: invoice.pdfS3Key || '',
      pdfUrl: invoice.pdfUrl || '',
      createdAt: invoice.createdAt ? invoice.createdAt.toISOString() : '',
      items: (invoice.items || []).map((item: any) => ({
        id: item.id,
        description: item.description,
        quantity: item.quantity || 1,
        unitPrice: item.unitPrice || item.amount,
        amount: item.amount,
        category: item.category || 'FREIGHT',
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
