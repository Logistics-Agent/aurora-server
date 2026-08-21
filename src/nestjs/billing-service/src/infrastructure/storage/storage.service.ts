import { Injectable, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';

@Injectable()
export class StorageService {
  private readonly logger = new Logger(StorageService.name);
  private readonly bucketName: string;

  constructor(private readonly configService: ConfigService) {
    this.bucketName = this.configService.get<string>('billing.s3BucketName', 'aurora-private-docs');
  }

  /**
   * Generates Multi-Tenant S3 Storage Key: tenants/{tenantId}/billing/invoices/{invoiceId}.pdf
   */
  generateInvoiceS3Key(tenantId: string, invoiceId: string): string {
    return `tenants/${tenantId}/billing/invoices/${invoiceId}.pdf`;
  }

  /**
   * Renders PDF Mock and returns Presigned Upload/Download URL
   */
  async renderAndUploadInvoicePdf(
    tenantId: string,
    invoiceId: string,
    invoiceNumber: string,
  ): Promise<{ s3Key: string; presignedUrl: string }> {
    const s3Key = this.generateInvoiceS3Key(tenantId, invoiceId);
    this.logger.log(`Rendering PDF for invoice ${invoiceNumber} -> S3 Key: s3://${this.bucketName}/${s3Key}`);

    const presignedUrl = `https://r2.aurora.io/${this.bucketName}/${s3Key}?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Expires=86400`;

    return {
      s3Key,
      presignedUrl,
    };
  }
}
