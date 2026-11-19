import { Injectable, OnModuleDestroy, ServiceUnavailableException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as grpc from '@grpc/grpc-js';
import * as protoLoader from '@grpc/proto-loader';
import { existsSync } from 'fs';
import { join } from 'path';
import type { ReadModelShipment } from '../../read-model/read-model.store';

interface ShipmentResponse {
  id: string;
  shipmentNo: string;
  tenantId: string;
  customerId: string;
  originAddress: string;
  destinationAddress: string;
  status: string;
  updatedAt?: { seconds: number | string; nanos?: number };
}

export interface BillingResponse {
  customerId: string;
  totalDebt: number;
  unpaidCount: number;
  invoices: Array<{
    invoiceId: string; invoiceNumber: string; totalAmount: number;
    remainingBalance: number; status: string; dueDate: string;
  }>;
}

@Injectable()
export class LiveReadModelGrpcClient implements OnModuleDestroy {
  private readonly shipmentClient: grpc.Client;
  private readonly billingClient: grpc.Client;

  constructor(config: ConfigService) {
    const shipmentProto = this.findProto('shipment_workflow.proto');
    const billingProto = this.findProto('billing.proto');
    const options = { keepCase: false, longs: String, enums: String, defaults: true };
    const shipmentService = grpc.loadPackageDefinition(protoLoader.loadSync(shipmentProto, options))
      .ShipmentWorkflowService as grpc.ServiceClientConstructor;
    const billingService = (grpc.loadPackageDefinition(protoLoader.loadSync(billingProto, options))
      .billing as grpc.GrpcObject).BillingService as grpc.ServiceClientConstructor;
    this.shipmentClient = new shipmentService(
      config.get<string>('SHIPMENT_WORKFLOW_GRPC_URL') || 'localhost:5001', grpc.credentials.createInsecure());
    this.billingClient = new billingService(
      config.get<string>('BILLING_GRPC_URL') || 'localhost:5004', grpc.credentials.createInsecure());
  }

  private findProto(name: string): string {
    const candidates = [
      join(process.cwd(), '../../../protos', name),
      join(process.cwd(), '../../protos', name),
      join(__dirname, '../../../../../protos', name),
    ];
    const path = candidates.find(existsSync);
    if (!path) throw new Error(`${name} is required by Customer Assistant`);
    return path;
  }

  async getShipment(shipmentId: string, tenantId: string): Promise<ReadModelShipment | undefined> {
    const metadata = new grpc.Metadata();
    metadata.set('x-tenant-id', tenantId);
    const client = this.shipmentClient as grpc.Client & {
      getShipment: (request: object, metadata: grpc.Metadata, options: grpc.CallOptions,
        callback: (error: grpc.ServiceError | null, response: ShipmentResponse) => void) => void;
    };
    const shipment = await new Promise<ShipmentResponse | undefined>((resolve, reject) => {
      client.getShipment({ id: shipmentId }, metadata, { deadline: Date.now() + 5000 }, (error, response) => {
        if (error?.code === grpc.status.NOT_FOUND) return resolve(undefined);
        if (error) return reject(new ServiceUnavailableException(`Shipment lookup failed: ${error.message}`));
        resolve(response);
      });
    });
    return shipment && shipment.tenantId === tenantId ? this.mapShipment(shipment) : undefined;
  }

  async getShipmentsByCustomer(customerId: string, tenantId: string): Promise<ReadModelShipment[]> {
    const response = await this.listShipments({ page: 1, limit: 100, customerId }, tenantId);
    return response.shipments
      .filter((shipment) => shipment.tenantId === tenantId && shipment.customerId.toLowerCase() === customerId.toLowerCase())
      .map((shipment) => this.mapShipment(shipment));
  }

  async getShipmentByNumber(shipmentNo: string, tenantId: string): Promise<ReadModelShipment | undefined> {
    const response = await this.listShipments({ page: 1, limit: 100, shipmentNo: shipmentNo.toUpperCase() }, tenantId);
    const shipment = response.shipments.find((item) =>
      item.tenantId === tenantId && item.shipmentNo.toLowerCase() === shipmentNo.toLowerCase());
    return shipment ? this.mapShipment(shipment) : undefined;
  }

  private async listShipments(request: object, tenantId: string): Promise<{ shipments: ShipmentResponse[] }> {
    const metadata = new grpc.Metadata();
    metadata.set('x-tenant-id', tenantId);
    const client = this.shipmentClient as grpc.Client & {
      listShipments: (request: object, metadata: grpc.Metadata, options: grpc.CallOptions,
        callback: (error: grpc.ServiceError | null, response: { shipments: ShipmentResponse[] }) => void) => void;
    };
    const response = await new Promise<{ shipments: ShipmentResponse[] }>((resolve, reject) => {
      client.listShipments(request, metadata,
        { deadline: Date.now() + 5000 }, (error, result) => error
          ? reject(new ServiceUnavailableException(`Shipment list failed: ${error.message}`)) : resolve(result));
    });
    return response;
  }

  async getCustomerBalanceSummary(customerId: string, tenantId: string): Promise<BillingResponse> {
    const metadata = new grpc.Metadata();
    metadata.set('x-tenant-id', tenantId);
    const client = this.billingClient as grpc.Client & {
      getCustomerBillingSummary: (request: object, metadata: grpc.Metadata, options: grpc.CallOptions,
        callback: (error: grpc.ServiceError | null, response: BillingResponse) => void) => void;
    };
    return new Promise((resolve, reject) => {
      client.getCustomerBillingSummary({ customerId }, metadata, { deadline: Date.now() + 5000 },
        (error, response) => error
          ? reject(new ServiceUnavailableException(`Billing summary failed: ${error.message}`)) : resolve(response));
    });
  }

  private mapShipment(shipment: ShipmentResponse): ReadModelShipment {
    return {
      shipmentId: shipment.id,
      shipmentNo: shipment.shipmentNo,
      tenantId: shipment.tenantId,
      customerId: shipment.customerId,
      originPort: shipment.originAddress,
      destinationPort: shipment.destinationAddress,
      status: shipment.status,
      updatedAt: shipment.updatedAt
        ? new Date(Number(shipment.updatedAt.seconds) * 1000).toISOString() : '',
    };
  }

  onModuleDestroy(): void {
    this.shipmentClient.close();
    this.billingClient.close();
  }
}
