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
    podDocumentS3Key?: string;
    completedAt: string;
}
export declare class ShipmentCompletedEventHandler {
    private readonly prisma;
    private readonly generateInvoiceUseCase;
    private readonly logger;
    constructor(prisma: PrismaService, generateInvoiceUseCase: GenerateInvoiceUseCase);
    handle(event: ShipmentCompletedEvent): Promise<void>;
}
