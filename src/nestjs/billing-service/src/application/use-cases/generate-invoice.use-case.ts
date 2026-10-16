import { BadRequestException, ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { PrismaService } from '../../infrastructure/prisma/prisma.service';
import { InvoiceDomainService } from '../../domain/services/invoice.domain-service';
import { ShipmentWorkflowGrpcClient } from '../../infrastructure/grpc-clients/shipment-workflow.grpc-client';
import { RabbitMQMessagingService } from '../../infrastructure/messaging/rabbitmq.service';
import { allocateInvoiceNumber } from '../../domain/services/invoice-number';

export interface GenerateInvoiceInput {
  tenantId: string;
  shipmentId: string;
}

@Injectable()
export class GenerateInvoiceUseCase {
  constructor(
    private readonly prisma: PrismaService,
    private readonly domainService: InvoiceDomainService,
    private readonly shipments: ShipmentWorkflowGrpcClient,
    private readonly messagingService: RabbitMQMessagingService,
  ) {}

  async execute(input: GenerateInvoiceInput) {
    if (!input.tenantId?.trim() || !input.shipmentId?.trim()) {
      throw new BadRequestException('Tenant and shipment are required');
    }
    const tenantId = input.tenantId.trim().toLowerCase();
    const shipmentId = input.shipmentId.trim().toLowerCase();

    const shipment = await this.shipments.getShipment(tenantId, shipmentId);
    if (shipment.id.toLowerCase() !== shipmentId || shipment.tenantId.toLowerCase() !== tenantId ||
        shipment.status !== 'Completed' || !shipment.customerId) {
      throw new BadRequestException('Shipment must be completed and have a verified customer');
    }
    const pod = shipment.documents.find((document) =>
      document.documentType === 'ProofOfDelivery' && document.storageUrl?.trim());
    if (!pod) throw new BadRequestException('Proof of Delivery is required before invoicing');

    const agreement = await this.prisma.shipmentPriceAgreement.findUnique({
      where: { tenantId_shipmentId: { tenantId, shipmentId } },
    });
    if (!agreement || agreement.customerId.toLowerCase() !== shipment.customerId.toLowerCase()) {
      throw new NotFoundException('Verified agreed price is required for this shipment and customer');
    }
    if (agreement.currency !== 'USD') throw new BadRequestException('Only USD invoices are supported');
    const account = await this.prisma.customerCreditAccount.findUnique({
      where: { tenantId_customerId: { tenantId, customerId: shipment.customerId.toLowerCase() } },
    });
    if (!account || account.currency !== agreement.currency) {
      throw new NotFoundException('Approved customer payment terms are required');
    }

    const subtotal = Number(agreement.subtotal.toFixed(2));
    const taxAmount = Number(agreement.taxAmount.toFixed(2));
    const totalAmount = Number(agreement.totalAmount.toFixed(2));
    if (!Number.isSafeInteger(Math.round(totalAmount * 100)) ||
        Math.round((subtotal + taxAmount) * 100) !== Math.round(totalAmount * 100)) {
      throw new BadRequestException('Agreed price is outside supported invoice range');
    }
    const dueDate = this.domainService.calculateDueDate(new Date(), account.termsDays);

    const createdInvoice = await this.prisma.$transaction(async (tx) => {
      const invoiceNumber = await allocateInvoiceNumber(tx, this.domainService);
      const duplicate = await tx.invoice.findFirst({
        where: { tenantId, shipmentId },
      });
      if (duplicate) throw new ConflictException(`Invoice ${duplicate.invoiceNumber} already exists for shipment`);
      return tx.invoice.create({
        data: {
          tenantId,
          shipmentId,
          customerId: shipment.customerId.toLowerCase(),
          agreementId: agreement.id,
          invoiceNumber,
          subtotal,
          taxAmount,
          totalAmount,
          currency: agreement.currency,
          status: 'UNPAID',
          dueDate,
          podS3Key: pod.storageUrl,
          items: {
            create: [{
              description: `Agreed logistics services (${agreement.evidenceReference})`,
              quantity: 1,
              unitPrice: subtotal,
              amount: subtotal,
              category: 'FREIGHT',
            }],
          },
        },
        include: { items: true },
      });
    });

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
