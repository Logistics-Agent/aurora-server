import { Injectable, Logger, ConflictException, BadRequestException } from '@nestjs/common';
import { PrismaService } from '../../infrastructure/prisma/prisma.service';
import { InvoiceDomainService } from '../../domain/services/invoice.domain-service';
import { FinancialGrpcClient } from '../../infrastructure/grpc-clients/financial.grpc-client';
import { RabbitMQMessagingService } from '../../infrastructure/messaging/rabbitmq.service';
import { ConfigService } from '@nestjs/config';
import { allocateInvoiceNumber } from '../../domain/services/invoice-number';

export interface GenerateInvoiceInput {
  tenantId: string;
  shipmentId: string;
  customerId?: string;
  originPort?: string;
  destinationPort?: string;
  weightKg?: number;
  volumeCbm?: number;
  paymentTermsDays?: number;
  podS3Key?: string;
}

@Injectable()
export class GenerateInvoiceUseCase {
  private readonly logger = new Logger(GenerateInvoiceUseCase.name);

  constructor(
    private readonly prisma: PrismaService,
    private readonly domainService: InvoiceDomainService,
    private readonly financialGrpcClient: FinancialGrpcClient,
    private readonly messagingService: RabbitMQMessagingService,
    private readonly configService: ConfigService,
  ) {}

  async execute(input: GenerateInvoiceInput) {
    if (!input.tenantId || !input.shipmentId || !input.customerId || !input.podS3Key ||
        !input.originPort || !input.destinationPort ||
        !Number.isFinite(input.weightKg) || input.weightKg <= 0 ||
        !Number.isFinite(input.volumeCbm) || input.volumeCbm < 0) {
      throw new BadRequestException('Tenant, shipment, customer, POD and measured route/cargo data are required');
    }
    this.logger.log(`Executing GenerateInvoiceUseCase for shipment ${input.shipmentId} (Tenant: ${input.tenantId})`);

    // ── 1. Idempotency Check ─────────────────────────────────────────────
    const existing = await this.prisma.invoice.findFirst({
      where: {
        tenantId: input.tenantId,
        shipmentId: input.shipmentId,
      },
    });

    if (existing) {
      throw new ConflictException(`Invoice ${existing.invoiceNumber} already generated for shipment ${input.shipmentId}`);
    }

    // ── 2. Inter-Service gRPC Call to FinancialService ───────────────────
    const costEstimate = await this.financialGrpcClient.estimateCost({
      tenantId: input.tenantId,
      originCountry: 'CN',
      originPort: input.originPort,
      destinationCountry: 'VN',
      destinationPort: input.destinationPort,
      weightKg: input.weightKg,
      volumeCbm: input.volumeCbm,
    });

    // ── 3. Build Invoice Line Items ──────────────────────────────────────
    const itemsInput = [
      {
        description: `Base Freight Charge (${input.originPort} -> ${input.destinationPort})`,
        quantity: 1,
        unitPrice: costEstimate.baseFreightCost,
        amount: costEstimate.baseFreightCost,
        category: 'FREIGHT',
      },
      {
        description: 'Port & Terminal Handling Charge (THC / DOC)',
        quantity: 1,
        unitPrice: costEstimate.portHandlingFees,
        amount: costEstimate.portHandlingFees,
        category: 'PORT_FEE',
      },
      {
        description: `Customs Duty & Import Tax (${costEstimate.description})`,
        quantity: 1,
        unitPrice: costEstimate.totalCustomsFee,
        amount: costEstimate.totalCustomsFee,
        category: 'CUSTOMS_DUTY',
      },
    ];

    const totals = this.domainService.calculateInvoiceTotals(itemsInput, 5.0);

    // ── 4. Generate Auto Invoice Number & Due Date (T+30) ─────────────────
    const paymentTermsDays =
      input.paymentTermsDays ||
      this.configService.get<number>('billing.defaultPaymentTermsDays', 30);
    const dueDate = this.domainService.calculateDueDate(new Date(), paymentTermsDays);

    // ── 5. Execute 1 ACID Database Transaction ───────────────────────────
    const createdInvoice = await this.prisma.$transaction(async (tx) => {
      const invoiceNumber = await allocateInvoiceNumber(tx, this.domainService);
      const duplicate = await tx.invoice.findFirst({
        where: { tenantId: input.tenantId, shipmentId: input.shipmentId },
      });
      if (duplicate) {
        throw new ConflictException(`Invoice ${duplicate.invoiceNumber} already generated for shipment ${input.shipmentId}`);
      }
      return tx.invoice.create({
        data: {
          tenantId: input.tenantId,
          shipmentId: input.shipmentId,
          customerId: input.customerId,
          invoiceNumber: invoiceNumber,
          subtotal: totals.subtotal,
          taxAmount: totals.taxAmount,
          totalAmount: totals.totalAmount,
          currency: costEstimate.currency || 'USD',
          status: 'UNPAID',
          dueDate: dueDate,
          podS3Key: input.podS3Key || null,
          items: {
            create: itemsInput.map((item) => ({
              description: item.description,
              quantity: item.quantity,
              unitPrice: item.unitPrice,
              amount: item.amount,
              category: item.category,
            })),
          },
        },
        include: {
          items: true,
        },
      });
    });

    // PDF fields remain empty until a real renderer and object-storage adapter are configured.
    await this.messagingService.publishInvoiceCreated({
      tenantId: createdInvoice.tenantId,
      invoiceId: createdInvoice.id,
      invoiceNumber: createdInvoice.invoiceNumber,
      shipmentId: createdInvoice.shipmentId,
      customerId: createdInvoice.customerId,
      totalAmount: createdInvoice.totalAmount,
      currency: createdInvoice.currency,
      dueDate: createdInvoice.dueDate.toISOString(),
      pdfUrl: '',
      createdAt: createdInvoice.createdAt.toISOString(),
    });

    return createdInvoice;
  }
}
