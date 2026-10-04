import * as assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { PrismaService } from '../src/infrastructure/prisma/prisma.service';
import { ShipmentQuoteService } from '../src/application/services/shipment-quote.service';
import { ShipmentWorkflowGrpcClient } from '../src/infrastructure/grpc-clients/shipment-workflow.grpc-client';

async function main() {
  const prisma = new PrismaService();
  const tenantId = randomUUID();
  const shipmentId = randomUUID();
  const customerId = randomUUID();
  const shipment = {
    getShipment: async () => ({ id: shipmentId, tenantId, customerId, status: 'Booked', documents: [] }),
  } as unknown as ShipmentWorkflowGrpcClient;
  const service = new ShipmentQuoteService(prisma, shipment);
  const validFrom = new Date(Date.now() - 60_000).toISOString();
  const validUntil = new Date(Date.now() + 86_400_000).toISOString();
  await prisma.$connect();
  try {
    const draft = await service.createDraft({
      tenantId, shipmentId, customerId, currency: 'USD', listPrice: '5000.00',
      floorPrice: '4200.00', evidenceReference: 'integration-tariff-001', validFrom, validUntil,
    }, 'creator');
    assert.equal(draft.status, 'DRAFT');
    await assert.rejects(service.getApproved({ tenantId, shipmentId, customerId }));
    await assert.rejects(service.approve({ tenantId, quoteId: draft.id }, 'creator'));
    const approved = await service.approve({ tenantId, quoteId: draft.id }, 'approver');
    assert.equal(approved.status, 'APPROVED');
    assert.equal((await service.getApproved({ tenantId, shipmentId, customerId })).id, draft.id);

    const replacement = await service.createDraft({
      tenantId, shipmentId, customerId, currency: 'USD', listPrice: '5100.00',
      floorPrice: '4300.00', evidenceReference: 'integration-tariff-002', validFrom, validUntil,
    }, 'creator');
    assert.equal(replacement.revision, 2);
    await service.approve({ tenantId, quoteId: replacement.id }, 'approver');
    const history = await service.list({ tenantId, shipmentId, customerId });
    assert.equal(history.quotes.find((quote) => quote.id === draft.id)?.status, 'SUPERSEDED');
    assert.equal((await service.getApproved({ tenantId, shipmentId, customerId })).id, replacement.id);
    await service.revoke({ tenantId, quoteId: replacement.id, reason: 'Tariff withdrawn' }, 'approver');
    await assert.rejects(service.getApproved({ tenantId, shipmentId, customerId }));
    console.log('Shipment quote integration passed');
  } finally {
    await prisma.shipmentApprovedQuote.deleteMany({ where: { tenantId, shipmentId } });
    await prisma.$disconnect();
  }
}

main().catch((error: unknown) => {
  console.error(error);
  process.exitCode = 1;
});
