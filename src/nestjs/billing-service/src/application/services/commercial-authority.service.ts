import {
  BadRequestException, ConflictException, ForbiddenException, Injectable,
  NotFoundException, ServiceUnavailableException,
} from '@nestjs/common';
import { Prisma, ShipmentPriceAgreement } from '@prisma/client';
import { PrismaService } from '../../infrastructure/prisma/prisma.service';
import { ShipmentWorkflowGrpcClient } from '../../infrastructure/grpc-clients/shipment-workflow.grpc-client';
import {
  CreditCheckRequest, CreditCheckResponse, CustomerCreditAccountResponse,
  CustomerBillingSummaryResponse, GetCustomerBillingSummaryRequest,
  GetShipmentAgreementRequest, RecordShipmentAgreementRequest,
  SetCustomerCreditAccountRequest, ShipmentAgreementResponse,
} from '../../interface/dto/billing.dto';

const MANAGE_PERMISSION = 'billing_settlement:settlement:manage';

@Injectable()
export class CommercialAuthorityService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly shipments: ShipmentWorkflowGrpcClient,
  ) {}

  private requireManager(actorUserId?: string, permissions?: string[]): string {
    if (!actorUserId || !permissions?.includes(MANAGE_PERMISSION)) {
      throw new ForbiddenException('Financial approval permission is required');
    }
    return actorUserId;
  }

  private money(value: string, field: string): Prisma.Decimal {
    if (!/^\d{1,16}(\.\d{1,2})?$/.test(value || '')) {
      throw new BadRequestException(`${field} must be a non-negative decimal with at most two fractional digits`);
    }
    const amount = new Prisma.Decimal(value);
    if (amount.mul(100).gt(Number.MAX_SAFE_INTEGER)) {
      throw new BadRequestException(`${field} exceeds the supported amount`);
    }
    return amount;
  }

  private requireUsd(currency?: string): void {
    if (currency?.toUpperCase() !== 'USD') {
      throw new BadRequestException('Only USD is supported until currency conversion is implemented');
    }
  }

  async recordAgreement(request: RecordShipmentAgreementRequest): Promise<ShipmentAgreementResponse> {
    const actorUserId = this.requireManager(request.actorUserId, request.permissions);
    const tenantId = request.tenantId?.trim().toLowerCase();
    const shipmentId = request.shipmentId?.trim().toLowerCase();
    const customerId = request.customerId?.trim().toLowerCase();
    const evidenceReference = request.evidenceReference?.trim();
    if (!tenantId || !shipmentId || !customerId || !evidenceReference) {
      throw new BadRequestException('Tenant, shipment, customer and agreement evidence are required');
    }
    this.requireUsd(request.currency);
    const subtotal = this.money(request.subtotal, 'subtotal');
    const taxAmount = this.money(request.taxAmount, 'taxAmount');
    const totalAmount = subtotal.add(taxAmount);
    if (subtotal.lte(0)) throw new BadRequestException('subtotal must be positive');
    if (totalAmount.mul(100).gt(Number.MAX_SAFE_INTEGER)) {
      throw new BadRequestException('totalAmount exceeds the supported amount');
    }

    const shipment = await this.shipments.getShipment(tenantId, shipmentId);
    if (shipment.id.toLowerCase() !== shipmentId ||
        shipment.tenantId.toLowerCase() !== tenantId || shipment.customerId.toLowerCase() !== customerId) {
      throw new BadRequestException('Shipment does not belong to the stated tenant and customer');
    }
    if (!['Confirmed', 'Delivered', 'Completed'].includes(shipment.status)) {
      throw new BadRequestException('Shipment must be confirmed before recording an agreed price');
    }

    const existing = await this.prisma.shipmentPriceAgreement.findUnique({
      where: { tenantId_shipmentId: { tenantId, shipmentId } },
    });
    if (existing) {
      if (existing.customerId !== customerId || !existing.subtotal.equals(subtotal) ||
          !existing.taxAmount.equals(taxAmount) || existing.currency !== 'USD' ||
          existing.evidenceReference !== evidenceReference) {
        throw new ConflictException('Shipment already has a different agreed price');
      }
      return this.mapAgreement(existing);
    }
    try {
      const agreement = await this.prisma.shipmentPriceAgreement.create({
        data: {
          tenantId, shipmentId, customerId, subtotal, taxAmount, totalAmount,
          currency: 'USD', evidenceReference, recordedBy: actorUserId,
        },
      });
      return this.mapAgreement(agreement);
    } catch (error) {
      if ((error as { code?: string }).code === 'P2002') {
        throw new ConflictException('Shipment already has an agreed price');
      }
      throw error;
    }
  }

  async getAgreement(request: GetShipmentAgreementRequest): Promise<ShipmentAgreementResponse> {
    if (!request.tenantId || !request.shipmentId) throw new BadRequestException('Tenant and shipment are required');
    const tenantId = request.tenantId.trim().toLowerCase();
    const shipmentId = request.shipmentId.trim().toLowerCase();
    const agreement = await this.prisma.shipmentPriceAgreement.findUnique({
      where: { tenantId_shipmentId: { tenantId, shipmentId } },
    });
    if (!agreement) throw new NotFoundException('No agreed price for shipment');
    return this.mapAgreement(agreement);
  }

  async setCreditAccount(request: SetCustomerCreditAccountRequest): Promise<CustomerCreditAccountResponse> {
    const actorUserId = this.requireManager(request.actorUserId, request.permissions);
    const tenantId = request.tenantId?.trim().toLowerCase();
    const customerId = request.customerId?.trim().toLowerCase();
    const reason = request.reason?.trim();
    if (!tenantId || !customerId || !reason) {
      throw new BadRequestException('Tenant, customer and approval reason are required');
    }
    this.requireUsd(request.currency);
    const creditLimit = this.money(request.creditLimit, 'creditLimit');
    if (!Number.isInteger(request.termsDays) || request.termsDays < 1 || request.termsDays > 365) {
      throw new BadRequestException('termsDays must be between 1 and 365');
    }
    const approvedAt = new Date();
    const account = await this.prisma.$transaction(async (tx) => {
      const saved = await tx.customerCreditAccount.upsert({
        where: { tenantId_customerId: { tenantId, customerId } },
        update: { creditLimit, termsDays: request.termsDays, onHold: request.onHold,
          approvedBy: actorUserId, approvedAt },
        create: { tenantId, customerId, creditLimit, currency: 'USD',
          termsDays: request.termsDays, onHold: request.onHold,
          approvedBy: actorUserId, approvedAt },
      });
      await tx.customerCreditLimitRevision.create({
        data: { accountId: saved.id, creditLimit, termsDays: request.termsDays,
          onHold: request.onHold, approvedBy: actorUserId, reason },
      });
      return saved;
    });
    return {
      customerId: account.customerId,
      creditLimit: account.creditLimit.toFixed(2),
      currency: account.currency,
      termsDays: account.termsDays,
      onHold: account.onHold,
      approvedBy: account.approvedBy,
      approvedAt: account.approvedAt.toISOString(),
    };
  }

  async checkCredit(request: CreditCheckRequest): Promise<CreditCheckResponse> {
    const tenantId = request.tenantId?.trim().toLowerCase();
    const customerId = request.customerId?.trim().toLowerCase();
    if (!tenantId || !customerId || !Number.isFinite(request.newAmount) || request.newAmount < 0 ||
        Math.round(request.newAmount * 100) / 100 !== request.newAmount) {
      throw new BadRequestException('Tenant, customer and a non-negative amount in cents are required');
    }
    const account = await this.prisma.customerCreditAccount.findUnique({
      where: { tenantId_customerId: { tenantId, customerId } },
    });
    if (!account) throw new ServiceUnavailableException('Customer credit limit has not been approved');
    const invoices = await this.prisma.invoice.findMany({
      where: { tenantId, customerId, status: { not: 'CANCELLED' } },
      include: { payments: true },
    });
    const now = new Date();
    let outstandingCents = 0;
    let overdueInvoiceCount = 0;
    for (const invoice of invoices) {
      if (invoice.currency !== account.currency) {
        throw new ServiceUnavailableException('Customer invoices contain unsupported mixed currencies');
      }
      const paidCents = invoice.payments
        .filter((payment) => payment.status === 'SUCCESS')
        .reduce((sum, payment) => sum + Math.round(payment.amountPaid * 100), 0);
      const remainingCents = Math.max(0, Math.round(invoice.totalAmount * 100) - paidCents);
      outstandingCents += remainingCents;
      if (remainingCents > 0 && invoice.dueDate < now) overdueInvoiceCount++;
    }
    const limitCents = Math.round(Number(account.creditLimit.toFixed(2)) * 100);
    const requestedCents = Math.round(request.newAmount * 100);
    const availableCents = Math.max(0, limitCents - outstandingCents);
    const isCreditApproved = !account.onHold && overdueInvoiceCount === 0 && requestedCents <= availableCents;
    return {
      customerId,
      isCreditApproved,
      creditLimit: limitCents / 100,
      currentOutstandingDebt: outstandingCents / 100,
      availableCredit: availableCents / 100,
      overdueInvoiceCount,
      message: account.onHold ? 'Customer credit is on hold'
        : overdueInvoiceCount > 0 ? 'Customer has overdue invoices'
        : isCreditApproved ? 'Credit check approved' : 'Credit limit exceeded',
    };
  }

  async getCustomerBillingSummary(request: GetCustomerBillingSummaryRequest): Promise<CustomerBillingSummaryResponse> {
    const tenantId = request.tenantId?.trim().toLowerCase();
    const customerId = request.customerId?.trim().toLowerCase();
    if (!tenantId || !customerId) throw new BadRequestException('Tenant and customer are required');
    const rows = await this.prisma.$queryRaw<Array<{
      id: string; invoice_number: string; total_amount: number;
      status: string; due_date: Date; paid_amount: number; currency: string;
    }>>`
      SELECT i.id, i.invoice_number, i.total_amount, i.status, i.due_date, i.currency,
             COALESCE(SUM(CASE WHEN p.status = 'SUCCESS' THEN p.amount_paid ELSE 0 END), 0) AS paid_amount
      FROM invoices AS i
      LEFT JOIN payment_records AS p ON p.invoice_id = i.id AND p.tenant_id = i.tenant_id
      WHERE i.tenant_id = ${tenantId} AND i.customer_id = ${customerId}
        AND i.status <> 'CANCELLED'
      GROUP BY i.id
      ORDER BY i.created_at DESC
    `;
    if (rows.some((row) => row.currency !== 'USD')) {
      throw new ServiceUnavailableException('Customer billing summary requires currency conversion');
    }
    const invoices = rows.map((row) => ({
      invoiceId: row.id,
      invoiceNumber: row.invoice_number,
      totalAmount: Math.round(Number(row.total_amount) * 100) / 100,
      remainingBalance: Math.max(0,
        Math.round(Number(row.total_amount) * 100) - Math.round(Number(row.paid_amount) * 100)) / 100,
      status: row.status,
      dueDate: row.due_date.toISOString(),
    }));
    return {
      customerId,
      totalDebt: Math.round(invoices.reduce((sum, invoice) => sum + invoice.remainingBalance * 100, 0)) / 100,
      unpaidCount: invoices.filter((invoice) => invoice.remainingBalance > 0).length,
      invoices: invoices.slice(0, 10),
    };
  }

  private mapAgreement(agreement: ShipmentPriceAgreement): ShipmentAgreementResponse {
    return {
      id: agreement.id,
      tenantId: agreement.tenantId,
      shipmentId: agreement.shipmentId,
      customerId: agreement.customerId,
      subtotal: agreement.subtotal.toFixed(2),
      taxAmount: agreement.taxAmount.toFixed(2),
      totalAmount: agreement.totalAmount.toFixed(2),
      currency: agreement.currency,
      evidenceReference: agreement.evidenceReference,
      recordedBy: agreement.recordedBy,
      recordedAt: agreement.recordedAt.toISOString(),
    };
  }
}
