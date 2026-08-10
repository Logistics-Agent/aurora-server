import { Injectable, Logger } from '@nestjs/common';
import { PrismaService } from '../../prisma/prisma.service';
import { GenerateInvoiceUseCase } from '../../../application/use-cases/generate-invoice.use-case';

export interface ShipmentCompletedEvent {
  tenantId: string;
  shipmentId: string;
  customerId?: string;
  originPort?: string;
  destinationPort?: string;
  weightKg?: number;
  volumeCbm?: number;
  podDocumentS3Key?: string; // TASK-004: Required for POD-triggered invoicing
  completedAt: string;
}

@Injectable()
export class ShipmentCompletedEventHandler {
  private readonly logger = new Logger(ShipmentCompletedEventHandler.name);

  constructor(
    private readonly prisma: PrismaService,
    private readonly generateInvoiceUseCase: GenerateInvoiceUseCase,
  ) {}

  /**
   * Idempotent Consumer: Handles shipment.pod_uploaded / shipment.completed events safely
   */
  async handle(event: ShipmentCompletedEvent): Promise<void> {
    this.logger.log(`Processing POD event for Shipment ID ${event.shipmentId} (Tenant: ${event.tenantId})`);

    // ── TASK-004 Validation: Check if POD document S3 key is provided ───
    if (!event.podDocumentS3Key) {
      this.logger.warn(
        `[POD Validation] Shipment ${event.shipmentId} event missing 'podDocumentS3Key'. Official invoice generation skipped until POD is uploaded.`,
      );
      return;
    }

    // ── Idempotency Check ────────────────────────────────────────────────
    const existingInvoice = await this.prisma.invoice.findFirst({
      where: {
        tenantId: event.tenantId,
        shipmentId: event.shipmentId,
      },
    });

    if (existingInvoice) {
      this.logger.warn(
        `[Idempotent Check] Invoice ${existingInvoice.invoiceNumber} already exists for shipment ${event.shipmentId}. Skipping event replay.`,
      );
      return;
    }

    // ── Auto Generate Invoice via UseCase ─────────────────────────────────
    await this.generateInvoiceUseCase.execute({
      tenantId: event.tenantId,
      shipmentId: event.shipmentId,
      customerId: event.customerId || 'CUST-001',
      originPort: event.originPort || 'SGSIN',
      destinationPort: event.destinationPort || 'VNSGN',
      weightKg: event.weightKg || 1000,
      volumeCbm: event.volumeCbm || 5,
      podS3Key: event.podDocumentS3Key,
    });
  }
}

