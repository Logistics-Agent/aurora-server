import { ConfigService } from '@nestjs/config';
import { ServiceUnavailableException } from '@nestjs/common';
import { ReadModelStore } from './read-model.store';

describe('ReadModelStore tenant boundary', () => {
  const productionConfig = { get: () => 'production' } as unknown as ConfigService;

  it('does not serve demo data in production', async () => {
    const store = new ReadModelStore(productionConfig);
    await expect(store.getShipmentsByCustomer('CUST-001', 'tenant-a'))
      .rejects.toThrow(ServiceUnavailableException);
  });

  it('keeps equal shipment IDs separate across tenants', async () => {
    const store = new ReadModelStore();
    const base = {
      shipmentId: 'shipment-1', shipmentNo: 'SHP-2026-001', customerId: 'customer-1', originPort: 'A',
      destinationPort: 'B', status: 'IN_TRANSIT', updatedAt: new Date().toISOString(),
    };
    store.upsertShipment({ ...base, tenantId: 'tenant-a' });
    store.upsertShipment({ ...base, tenantId: 'tenant-b', status: 'DELIVERED' });

    expect((await store.getShipment('shipment-1', 'tenant-a'))?.status).toBe('IN_TRANSIT');
    expect((await store.getShipment('shipment-1', 'tenant-b'))?.status).toBe('DELIVERED');
    expect(await store.getShipment('shipment-1', 'tenant-c')).toBeUndefined();
    expect((await store.getShipmentByNumber('shp-2026-001', 'tenant-a'))?.status).toBe('IN_TRANSIT');
    expect(await store.getShipmentByNumber('SHP-2026-001', 'tenant-c')).toBeUndefined();
  });

  it('reads the authoritative services in production', async () => {
    const shipment = { shipmentId: 'shipment-1', tenantId: 'tenant-a', customerId: 'customer-1' };
    const live = {
      getShipment: jest.fn().mockResolvedValue(shipment),
      getShipmentsByCustomer: jest.fn().mockResolvedValue([shipment]),
      getCustomerBalanceSummary: jest.fn().mockResolvedValue({
        customerId: 'customer-1', totalDebt: 25, unpaidCount: 1, invoices: [],
      }),
    } as any;
    const store = new ReadModelStore(productionConfig, live);
    expect(await store.getShipment('shipment-1', 'tenant-a')).toEqual(shipment);
    expect(await store.getShipmentsByCustomer('customer-1', 'tenant-a')).toEqual([shipment]);
    expect((await store.getCustomerBalanceSummary('customer-1', 'tenant-a')).totalDebt).toBe(25);
    expect(live.getCustomerBalanceSummary).toHaveBeenCalledWith('customer-1', 'tenant-a');
  });
});
