import { Injectable, Logger, ConflictException } from '@nestjs/common';
import { PrismaService } from '../../infrastructure/prisma/prisma.service';
import { InvoiceDomainService } from '../../domain/services/invoice.domain-service';
import { FinancialGrpcClient } from '../../infrastructure/grpc-clients/financial.grpc-client';
import { StorageService } from '../../infrastructure/storage/storage.service';
import { RabbitMQMessagingService } from '../../infrastructure/messaging/rabbitmq.service';
import { ConfigService } from '@nestjs/config';

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
    private readonly storageService: StorageService,
    private readonly messagingService: RabbitMQMessagingService,
    private readonly configService: ConfigService,
  ) {}

  async execute(input: GenerateInvoiceInput) {
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
      originPort: input.originPort || 'SGSIN',
      destinationCountry: 'VN',
      destinationPort: input.destinationPort || 'VNSGN',
      weightKg: input.weightKg || 1000,
      volumeCbm: input.volumeCbm || 5,
    });

    // ── 3. Build Invoice Line Items ──────────────────────────────────────
    const itemsInput = [
      {
        description: `Base Freight Charge (${input.originPort || 'SGSIN'} -> ${input.destinationPort || 'VNSGN'})`,
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
    const invoiceCount = await this.prisma.invoice.count({
      where: { tenantId: input.tenantId },
    });
    const invoiceNumber = this.domainService.generateInvoiceNumber(invoiceCount + 1);

    const paymentTermsDays =
      input.paymentTermsDays ||
      this.configService.get<number>('billing.defaultPaymentTermsDays', 30);
    const dueDate = this.domainService.calculateDueDate(new Date(), paymentTermsDays);

    // ── 5. Execute 1 ACID Database Transaction ───────────────────────────
    const createdInvoice = await this.prisma.$transaction(async (tx) => {
      return tx.invoice.create({
        data: {
          tenantId: input.tenantId,
          shipmentId: input.shipmentId,
          customerId: input.customerId || 'CUST-001',
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

    // ── 6. Render PDF & Generate Storage Key Presigned URL ────────────────
    const pdfResult = await this.storageService.renderAndUploadInvoicePdf(
      input.tenantId,
      createdInvoice.id,
      createdInvoice.invoiceNumber,
    );

    const updatedInvoice = await this.prisma.invoice.update({
      where: { id: createdInvoice.id },
      data: {
        pdfS3Key: pdfResult.s3Key,
        pdfUrl: pdfResult.presignedUrl,
      },
      include: { items: true },
    });

    // ── 7. Publish Event to RabbitMQ ─────────────────────────────────────
    await this.messagingService.publishInvoiceCreated({
      tenantId: updatedInvoice.tenantId,
      invoiceId: updatedInvoice.id,
      invoiceNumber: updatedInvoice.invoiceNumber,
      shipmentId: updatedInvoice.shipmentId,
      customerId: updatedInvoice.customerId,
      totalAmount: updatedInvoice.totalAmount,
      currency: updatedInvoice.currency,
      dueDate: updatedInvoice.dueDate.toISOString(),
      pdfUrl: updatedInvoice.pdfUrl || '',
      createdAt: updatedInvoice.createdAt.toISOString(),
    });

    return updatedInvoice;
  }
}
