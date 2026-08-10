import { ConfigService } from '@nestjs/config';
export declare class StorageService {
    private readonly configService;
    private readonly logger;
    private readonly bucketName;
    constructor(configService: ConfigService);
    generateInvoiceS3Key(tenantId: string, invoiceId: string): string;
    renderAndUploadInvoicePdf(tenantId: string, invoiceId: string, invoiceNumber: string): Promise<{
        s3Key: string;
        presignedUrl: string;
    }>;
}
