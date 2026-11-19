import { ForbiddenException } from '@nestjs/common';
import { ActorType } from '../../domain/enums/actor-type.enum';
import { ReadModelStore } from '../../read-model/read-model.store';
import { ShipmentLookupTool } from './shipment-lookup.tool';
import { BillingSummaryTool } from './billing-summary.tool';

describe('Authoritative domain tools', () => {
  const customerId = '4c9b3fd2-5a5c-42c8-9181-f608dbf94e21';
  const otherCustomerId = 'f75ab743-4f7c-4c07-a59c-88bbd12ab640';
  const store = {
    getShipment: jest.fn(),
    getShipmentsByCustomer: jest.fn(),
    getCustomerBalanceSummary: jest.fn(),
  } as unknown as ReadModelStore;

  beforeEach(() => jest.clearAllMocks());

  it('requires shipment permission for staff before querying', async () => {
    const tool = new ShipmentLookupTool(store);
    const currentUser = { actorType: ActorType.STAFF, tenantId: 'tenant-1',
      customerId, userId: 'staff-1', roles: [], permissions: [] };
    await expect(tool.execute({ currentUser, conversationId: 'conv-1' }, { shipmentId: 'shipment-1' }))
      .rejects.toThrow(ForbiddenException);
    expect(store.getShipment).not.toHaveBeenCalled();
  });

  it('keeps customer shipment lookup within the authenticated customer', async () => {
    (store.getShipment as jest.Mock).mockResolvedValue({
      shipmentId: 'shipment-1', tenantId: 'tenant-1', customerId: otherCustomerId,
      originPort: 'A', destinationPort: 'B', status: 'Completed', updatedAt: '',
    });
    const tool = new ShipmentLookupTool(store);
    const currentUser = { actorType: ActorType.CUSTOMER, tenantId: 'tenant-1',
      customerId, userId: 'customer-1', roles: [], permissions: [] };
    const result = await tool.execute({ currentUser, conversationId: 'conv-1' }, { shipmentId: 'shipment-1' });
    expect(result.success).toBe(false);
  });

  it('requires billing permission and scopes the target to the tenant', async () => {
    const tool = new BillingSummaryTool(store);
    const currentUser = { actorType: ActorType.STAFF, tenantId: 'tenant-1',
      userId: 'staff-1', roles: [], permissions: [] };
    await expect(tool.execute({ currentUser, conversationId: 'conv-1' }, { targetCustomerId: customerId }))
      .rejects.toThrow(ForbiddenException);
    currentUser.permissions.push('billing_settlement:read');
    (store.getCustomerBalanceSummary as jest.Mock).mockResolvedValue({
      totalDebt: 0, unpaidCount: 0, invoices: [],
    });
    await tool.execute({ currentUser, conversationId: 'conv-1' }, { targetCustomerId: customerId });
    expect(store.getCustomerBalanceSummary).toHaveBeenCalledWith(customerId, 'tenant-1');
  });
});
