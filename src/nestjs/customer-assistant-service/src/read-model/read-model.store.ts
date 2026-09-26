import { Injectable, Logger, ServiceUnavailableException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';

export interface ReadModelShipment {
  shipmentId: string;
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
  private readonly isProduction: boolean;
  private hasExternalData = false;

  constructor(configService?: ConfigService) {
    this.isProduction = configService?.get<string>('NODE_ENV') === 'production';
    if (this.isProduction) return;
    // Seed mock read-model data for testing
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
    this.hasExternalData = true;
    this.shipments.set(`${shipment.tenantId}:${shipment.shipmentId}`, shipment);
    this.logger.log(`[ReadModel] Upserted shipment ${shipment.shipmentId} (Status: ${shipment.status})`);
  }

  upsertInvoice(invoice: ReadModelInvoice): void {
    this.hasExternalData = true;
    this.invoices.set(`${invoice.tenantId}:${invoice.invoiceId}`, invoice);
    this.logger.log(`[ReadModel] Upserted invoice ${invoice.invoiceNumber} (Status: ${invoice.status})`);
  }

  getShipment(shipmentId: string, tenantId: string): ReadModelShipment | undefined {
    this.ensureReady();
    const shipment = this.shipments.get(`${tenantId}:${shipmentId}`);
    return shipment?.tenantId === tenantId ? shipment : undefined;
  }

  getShipmentsByCustomer(customerId: string, tenantId: string): ReadModelShipment[] {
    this.ensureReady();
    return Array.from(this.shipments.values()).filter((s) => s.tenantId === tenantId && s.customerId === customerId);
  }

  getInvoicesByCustomer(customerId: string, tenantId: string): ReadModelInvoice[] {
    this.ensureReady();
    return Array.from(this.invoices.values()).filter((i) => i.tenantId === tenantId && i.customerId === customerId);
  }

  getCustomerBalanceSummary(customerId: string, tenantId: string) {
    const invoices = this.getInvoicesByCustomer(customerId, tenantId);
    const totalDebt = invoices.reduce((sum, inv) => sum + inv.remainingBalance, 0);
    const unpaidCount = invoices.filter((inv) => inv.status !== 'PAID').length;
    return {
      customerId,
      totalDebt: Number(totalDebt.toFixed(2)),
      unpaidCount,
      invoices,
    };
  }

  private ensureReady(): void {
    if (this.isProduction && !this.hasExternalData) {
      throw new ServiceUnavailableException('Assistant read model is not connected to live shipment and billing data');
    }
  }
}
