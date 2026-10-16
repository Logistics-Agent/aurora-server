import { Injectable, OnModuleDestroy } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as grpc from '@grpc/grpc-js';
import * as protoLoader from '@grpc/proto-loader';
import { existsSync } from 'fs';
import { join } from 'path';

export interface ShipmentSnapshot {
  id: string;
  tenantId: string;
  customerId: string;
  status: string;
  documents: Array<{ documentType: string; storageUrl: string }>;
}

@Injectable()
export class ShipmentWorkflowGrpcClient implements OnModuleDestroy {
  private readonly client: grpc.Client & {
    getShipment(
      request: { id: string },
      metadata: grpc.Metadata,
      options: grpc.CallOptions,
      callback: (error: grpc.ServiceError | null, response: ShipmentSnapshot) => void,
    ): void;
  };

  constructor(config: ConfigService) {
    const candidates = [
      join(process.cwd(), '../../../protos/shipment_workflow.proto'),
      join(process.cwd(), '../../protos/shipment_workflow.proto'),
      join(__dirname, '../../../../../protos/shipment_workflow.proto'),
    ];
    const protoPath = candidates.find(existsSync);
    if (!protoPath) throw new Error('shipment_workflow.proto is required by Billing');
    const definition = protoLoader.loadSync(protoPath, {
      keepCase: false, longs: String, enums: String, defaults: true,
    });
    const service = grpc.loadPackageDefinition(definition).ShipmentWorkflowService as grpc.ServiceClientConstructor;
    this.client = new service(
      config.get<string>('grpc.shipmentWorkflowUrl') || 'localhost:5001',
      grpc.credentials.createInsecure(),
    ) as unknown as typeof this.client;
  }

  getShipment(tenantId: string, shipmentId: string): Promise<ShipmentSnapshot> {
    const metadata = new grpc.Metadata();
    metadata.set('x-tenant-id', tenantId);
    return new Promise((resolve, reject) => {
      this.client.getShipment(
        { id: shipmentId }, metadata, { deadline: Date.now() + 5000 },
        (error, response) => error ? reject(error) : resolve(response),
      );
    });
  }

  onModuleDestroy(): void {
    this.client.close();
  }
}
