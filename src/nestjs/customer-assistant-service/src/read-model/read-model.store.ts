import { Injectable, Logger, Optional, ServiceUnavailableException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { BillingResponse, LiveReadModelGrpcClient } from '../infrastructure/grpc/live-read-model.grpc-client';

export interface ReadModelShipment {
  shipmentId: string;
  shipmentNo?: string;
  tenantId: string;
  customerId: string;
  originPort: string;
  destinationPort: string;
  status: string; // CREATED, IN_TRANSIT, DELIVERED, COMPLETED
  currentLocation?: string;
  updatedAt: string;
}

export interface ReadModelInvoice {
  invoiceId: string;
  tenantId: string;
  customerId: string;
  invoiceNumber: string;
  totalAmount: number;
  remainingBalance: number;
  status: string; // UNPAID, PARTIALLY_PAID, PAID
  dueDate: string;
  updatedAt: string;
}

@Injectable()
export class ReadModelStore {
  private readonly logger = new Logger(ReadModelStore.name);

  private readonly shipments: Map<string, ReadModelShipment> = new Map();
  private readonly invoices: Map<string, ReadModelInvoice> = new Map();
  private readonly useLiveData: boolean;

  constructor(@Optional() configService?: ConfigService,
              @Optional() private readonly live?: LiveReadModelGrpcClient) {
    this.useLiveData = Boolean(configService && configService.get<string>('NODE_ENV') !== 'test');
    if (this.useLiveData) return;
    // Unit tests can use a local read model. Runtime always queries source services.
    this.upsertShipment({
      shipmentId: 'shp_33019284',
      tenantId: 'a0000000-0000-0000-0000-000000000001',
      customerId: 'CUST-001',
      originPort: 'SGSIN',
      destinationPort: 'VNSGN',
      status: 'IN_TRANSIT',
      currentLocation: 'Vùng biển Biển Đông, đang hướng về Cảng Cát Lái (VNSGN)',
      updatedAt: new Date().toISOString(),
    });

    this.upsertInvoice({
      invoiceId: 'inv_77123940',
      tenantId: 'a0000000-0000-0000-0000-000000000001',
      customerId: 'CUST-001',
      invoiceNumber: 'INV-202608-0089',
      totalAmount: 1500.0,
      remainingBalance: 1000.0,
      status: 'PARTIALLY_PAID',
      dueDate: new Date(Date.now() + 15 * 86400000).toISOString(),
      updatedAt: new Date().toISOString(),
    });
  }

  upsertShipment(shipment: ReadModelShipment): void {
    this.shipments.set(`${shipment.tenantId}:${shipment.shipmentId}`, shipment);
    this.logger.log(`[ReadModel] Upserted shipment ${shipment.shipmentId} (Status: ${shipment.status})`);
  }

  upsertInvoice(invoice: ReadModelInvoice): void {
    this.invoices.set(`${invoice.tenantId}:${invoice.invoiceId}`, invoice);
    this.logger.log(`[ReadModel] Upserted invoice ${invoice.invoiceNumber} (Status: ${invoice.status})`);
  }

  async getShipment(shipmentId: string, tenantId: string): Promise<ReadModelShipment | undefined> {
    if (this.useLiveData) return this.requireLive().getShipment(shipmentId, tenantId);
    const shipment = this.shipments.get(`${tenantId}:${shipmentId}`);
    return shipment?.tenantId === tenantId ? shipment : undefined;
  }

  async getShipmentByNumber(shipmentNo: string, tenantId: string): Promise<ReadModelShipment | undefined> {
    if (this.useLiveData) return this.requireLive().getShipmentByNumber(shipmentNo, tenantId);
    return Array.from(this.shipments.values()).find((shipment) =>
      shipment.tenantId === tenantId && shipment.shipmentNo?.toLowerCase() === shipmentNo.toLowerCase());
  }

  async getShipmentsByCustomer(customerId: string, tenantId: string): Promise<ReadModelShipment[]> {
    if (this.useLiveData) return this.requireLive().getShipmentsByCustomer(customerId, tenantId);
    return Array.from(this.shipments.values()).filter((s) => s.tenantId === tenantId && s.customerId === customerId);
  }

  async getInvoicesByCustomer(customerId: string, tenantId: string): Promise<ReadModelInvoice[]> {
    if (this.useLiveData) {
      const summary = await this.requireLive().getCustomerBalanceSummary(customerId, tenantId);
      return summary.invoices.map((invoice) => ({ ...invoice, tenantId, customerId,
        updatedAt: '',
      }));
    }
    return Array.from(this.invoices.values()).filter((i) => i.tenantId === tenantId && i.customerId === customerId);
  }

  async getCustomerBalanceSummary(customerId: string, tenantId: string): Promise<BillingResponse> {
    if (this.useLiveData) return this.requireLive().getCustomerBalanceSummary(customerId, tenantId);
    const invoices = await this.getInvoicesByCustomer(customerId, tenantId);
    const totalDebt = invoices.reduce((sum, inv) => sum + inv.remainingBalance, 0);
    const unpaidCount = invoices.filter((inv) => inv.status !== 'PAID').length;
    return {
      customerId,
      totalDebt: Number(totalDebt.toFixed(2)),
      unpaidCount,
      invoices,
    };
  }

  private requireLive(): LiveReadModelGrpcClient {
    if (!this.live) throw new ServiceUnavailableException('Assistant live data clients are not configured');
    return this.live;
  }
}
