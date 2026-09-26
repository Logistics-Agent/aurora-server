import { ConfigService } from '@nestjs/config';
import { ServiceUnavailableException } from '@nestjs/common';
import { ReadModelStore } from './read-model.store';

describe('ReadModelStore tenant boundary', () => {
  const productionConfig = { get: () => 'production' } as unknown as ConfigService;

  it('does not serve demo data in production', () => {
    const store = new ReadModelStore(productionConfig);
    expect(() => store.getShipmentsByCustomer('CUST-001', 'tenant-a'))
      .toThrow(ServiceUnavailableException);
  });

  it('keeps equal shipment IDs separate across tenants', () => {
    const store = new ReadModelStore(productionConfig);
    const base = {
      shipmentId: 'shipment-1', customerId: 'customer-1', originPort: 'A',
      destinationPort: 'B', status: 'IN_TRANSIT', updatedAt: new Date().toISOString(),
    };
    store.upsertShipment({ ...base, tenantId: 'tenant-a' });
    store.upsertShipment({ ...base, tenantId: 'tenant-b', status: 'DELIVERED' });

    expect(store.getShipment('shipment-1', 'tenant-a')?.status).toBe('IN_TRANSIT');
    expect(store.getShipment('shipment-1', 'tenant-b')?.status).toBe('DELIVERED');
    expect(store.getShipment('shipment-1', 'tenant-c')).toBeUndefined();
  });
});
