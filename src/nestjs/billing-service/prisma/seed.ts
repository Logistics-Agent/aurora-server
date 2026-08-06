import { PrismaClient } from '@prisma/client';

const prisma = new PrismaClient();

async function main() {
  console.log('Seeding billing database...');

  const mockTenantId = 'a0000000-0000-0000-0000-000000000001';
  const mockCarrierId = 'c0000000-0000-0000-0000-000000000001';

  // Seed Mock Wallet
  const wallet = await prisma.escrowWallet.upsert({
    where: {
      tenantId_carrierId: {
        tenantId: mockTenantId,
        carrierId: mockCarrierId,
      },
    },
    update: {},
    create: {
      id: 'w0000000-0000-0000-0000-000000000001',
      tenantId: mockTenantId,
      carrierId: mockCarrierId,
      balance: 10000.0,
      frozenAmount: 1500.0,
      currency: 'USD',
    },
  });

  // Seed Initial Transaction
  const existingTx = await prisma.escrowTransaction.findFirst({
    where: { walletId: wallet.id },
  });

  if (!existingTx) {
    await prisma.escrowTransaction.create({
      data: {
        id: 't0000000-0000-0000-0000-000000000001',
        walletId: wallet.id,
        shipmentId: 'shipment-101',
        type: 'FREEZE',
        amount: 1500.0,
        status: 'SUCCESS',
        referenceNo: 'REF-SHIP-101',
      },
    });
  }

  // Seed Mock Invoice
  const existingInvoice = await prisma.invoice.findUnique({
    where: { invoiceNumber: 'INV-2026-0001' },
  });

  if (!existingInvoice) {
    await prisma.invoice.create({
      data: {
        id: 'i0000000-0000-0000-0000-000000000001',
        tenantId: mockTenantId,
        shipmentId: 'shipment-101',
        invoiceNumber: 'INV-2026-0001',
        subtotal: 1800.0,
        taxAmount: 90.0,
        totalAmount: 1890.0,
        status: 'UNPAID',
        dueDate: new Date(Date.now() + 14 * 24 * 60 * 60 * 1000),
        items: {
          create: [
            {
              description: 'Base Freight Charge (SGSIN -> VNSGN)',
              amount: 1500.0,
              category: 'FREIGHT',
            },
            {
              description: 'Port Handling Fee',
              amount: 300.0,
              category: 'PORT_FEE',
            },
            {
              description: 'Import Customs Duty 5%',
              amount: 90.0,
              category: 'CUSTOMS_DUTY',
            },
          ],
        },
      },
    });
  }

  console.log('Billing seeding completed successfully.');
}

main()
  .catch((e) => {
    console.error(e);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });
