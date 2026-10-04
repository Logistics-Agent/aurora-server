import { Injectable, OnModuleDestroy, ServiceUnavailableException, NotFoundException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as grpc from '@grpc/grpc-js';
import * as protoLoader from '@grpc/proto-loader';
import { existsSync } from 'fs';
import { join } from 'path';

export interface ApprovedShipmentQuote {
  id: string;
  tenantId: string;
  shipmentId: string;
  customerId: string;
  revision: number;
  currency: string;
  listPrice: string;
  floorPrice: string;
  evidenceReference: string;
  validFrom: string;
  validUntil: string;
  status: string;
  approvedBy: string;
  approvedAt: string;
}

@Injectable()
export class BillingQuoteGrpcClient implements OnModuleDestroy {
  private readonly client: grpc.Client & {
    getApprovedShipmentQuote(
      request: { shipmentId: string; customerId: string },
      metadata: grpc.Metadata,
      options: grpc.CallOptions,
      callback: (error: grpc.ServiceError | null, response: ApprovedShipmentQuote) => void,
    ): void;
  };

  constructor(private readonly config: ConfigService) {
    const candidates = [
      '/protos/billing.proto',
      join(process.cwd(), '../../../protos/billing.proto'),
      join(process.cwd(), '../../protos/billing.proto'),
    ];
    const protoPath = candidates.find(existsSync);
    if (!protoPath) throw new Error('billing.proto is required by Negotiation');
    const definition = protoLoader.loadSync(protoPath, {
      keepCase: false, longs: String, enums: String, defaults: true,
    });
    const service = (grpc.loadPackageDefinition(definition).billing as grpc.GrpcObject)
      .BillingService as grpc.ServiceClientConstructor;
    this.client = new service(
      this.config.get<string>('BILLING_GRPC_URL') || 'localhost:5004',
      grpc.credentials.createInsecure(),
    ) as unknown as typeof this.client;
  }

  getApproved(tenantId: string, shipmentId: string, customerId: string): Promise<ApprovedShipmentQuote> {
    const secret = this.config.get<string>('INTERNAL_SERVICE_SECRET');
    if (!secret && this.config.get<string>('NODE_ENV') === 'production') {
      throw new ServiceUnavailableException('Billing service credentials are not configured');
    }
    const metadata = new grpc.Metadata();
    metadata.set('x-tenant-id', tenantId);
    if (secret) metadata.set('x-internal-secret', secret);
    return new Promise((resolve, reject) => {
      this.client.getApprovedShipmentQuote(
        { shipmentId, customerId }, metadata, { deadline: Date.now() + 5000 },
        (error, response) => {
          if (error?.code === grpc.status.NOT_FOUND) {
            reject(new NotFoundException('No approved, current quote for this shipment and customer'));
          } else if (error) {
            reject(new ServiceUnavailableException('Approved quote lookup is unavailable'));
          } else {
            resolve(response);
          }
        },
      );
    });
  }

  onModuleDestroy(): void {
    this.client.close();
  }
}
