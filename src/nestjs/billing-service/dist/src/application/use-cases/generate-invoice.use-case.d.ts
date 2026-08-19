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
export declare class GenerateInvoiceUseCase {
    private readonly prisma;
    private readonly domainService;
    private readonly financialGrpcClient;
    private readonly storageService;
    private readonly messagingService;
    private readonly configService;
    private readonly logger;
    constructor(prisma: PrismaService, domainService: InvoiceDomainService, financialGrpcClient: FinancialGrpcClient, storageService: StorageService, messagingService: RabbitMQMessagingService, configService: ConfigService);
    execute(input: GenerateInvoiceInput): Promise<{
        items: {
            id: string;
            amount: number;
            description: string;
            quantity: number;
            unitPrice: number;
            category: string;
            invoiceId: string;
        }[];
    } & {
        id: string;
        tenantId: string;
        currency: string;
        createdAt: Date;
        updatedAt: Date;
        shipmentId: string;
        status: string;
        invoiceNumber: string;
        customerId: string;
        subtotal: number;
        taxAmount: number;
        totalAmount: number;
        dueDate: Date;
        pdfS3Key: string | null;
        pdfUrl: string | null;
        podS3Key: string | null;
    }>;
}
