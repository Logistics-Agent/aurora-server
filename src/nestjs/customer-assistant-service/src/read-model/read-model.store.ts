import { Injectable, Logger } from '@nestjs/common';

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

  constructor() {
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
    this.shipments.set(shipment.shipmentId, shipment);
    this.logger.log(`[ReadModel] Upserted shipment ${shipment.shipmentId} (Status: ${shipment.status})`);
  }

  upsertInvoice(invoice: ReadModelInvoice): void {
    this.invoices.set(invoice.invoiceId, invoice);
    this.logger.log(`[ReadModel] Upserted invoice ${invoice.invoiceNumber} (Status: ${invoice.status})`);
  }

  getShipment(shipmentId: string): ReadModelShipment | undefined {
    return this.shipments.get(shipmentId);
  }

  getShipmentsByCustomer(customerId: string): ReadModelShipment[] {
    return Array.from(this.shipments.values()).filter((s) => s.customerId === customerId);
  }

  getInvoicesByCustomer(customerId: string): ReadModelInvoice[] {
    return Array.from(this.invoices.values()).filter((i) => i.customerId === customerId);
  }

  getCustomerBalanceSummary(customerId: string) {
    const invoices = this.getInvoicesByCustomer(customerId);
    const totalDebt = invoices.reduce((sum, inv) => sum + inv.remainingBalance, 0);
    const unpaidCount = invoices.filter((inv) => inv.status !== 'PAID').length;
    return {
      customerId,
      totalDebt: Number(totalDebt.toFixed(2)),
      unpaidCount,
      invoices,
    };
  }
}
