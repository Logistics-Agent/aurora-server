import { PrismaClient } from '@prisma/client';

const prisma = new PrismaClient();

async function main() {
  console.log('Seeding billing database with Clean Architecture schema...');

  const mockTenantId = 'a0000000-0000-0000-0000-000000000001';
  const mockCarrierId = 'c0000000-0000-0000-0000-000000000001';
  const mockCustomerId = 'CUST-001';

  // 1. Seed Escrow Wallet
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

  // 2. Seed Mock Invoice with Line Items & Payment Record
  const existingInvoice = await prisma.invoice.findUnique({
    where: { invoiceNumber: 'INV-202607-0001' },
  });

  if (!existingInvoice) {
    await prisma.invoice.create({
      data: {
        id: 'i0000000-0000-0000-0000-000000000001',
        tenantId: mockTenantId,
        shipmentId: 'shipment-101',
        customerId: mockCustomerId,
        invoiceNumber: 'INV-202607-0001',
        subtotal: 1800.0,
        taxAmount: 90.0,
        totalAmount: 1890.0,
        currency: 'USD',
        status: 'PARTIALLY_PAID',
        dueDate: new Date(Date.now() + 14 * 24 * 60 * 60 * 1000),
        pdfS3Key: `tenants/${mockTenantId}/billing/invoices/i0000000-0000-0000-0000-000000000001.pdf`,
        pdfUrl: `https://r2.aurora.io/aurora-private-docs/tenants/${mockTenantId}/billing/invoices/i0000000-0000-0000-0000-000000000001.pdf`,
        items: {
          create: [
            {
              description: 'Base Freight Charge (SGSIN -> VNSGN)',
              quantity: 1,
              unitPrice: 1500.0,
              amount: 1500.0,
              category: 'FREIGHT',
            },
            {
              description: 'Port Handling Fee (THC / DOC)',
              quantity: 1,
              unitPrice: 300.0,
              amount: 300.0,
              category: 'PORT_FEE',
            },
          ],
        },
        payments: {
          create: [
            {
              id: 'p0000000-0000-0000-0000-000000000001',
              tenantId: mockTenantId,
              amountPaid: 1000.0,
              paymentMethod: 'BANK_TRANSFER',
              transactionRef: 'TX-BANK-8899',
              status: 'SUCCESS',
            },
          ],
        },
      },
    });
  }

  console.log('Billing database seeding completed successfully.');
}

main()
  .catch((e) => {
    console.error(e);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });
