import { PrismaClient } from '@prisma/client';

const prisma = new PrismaClient();

async function main() {
  console.log('Seeding financial database with Clean Architecture schema...');

  const mockTenantId = 'a0000000-0000-0000-0000-000000000001';

  // 1. Seed Customs Duty Rates
  await prisma.customsDutyRate.upsert({
    where: {
      tenantId_hsCode: {
        tenantId: mockTenantId,
        hsCode: '8517.12.00',
      },
    },
    update: {},
    create: {
      id: 'f0000000-0000-0000-0000-000000000001',
      tenantId: mockTenantId,
      hsCode: '8517.12.00',
      description: 'Mobile phones import duty & VAT',
      importTaxRate: 5.0,
      vatRate: 10.0,
    },
  });

  await prisma.customsDutyRate.upsert({
    where: {
      tenantId_hsCode: {
        tenantId: mockTenantId,
        hsCode: '8471.30.10',
      },
    },
    update: {},
    create: {
      id: 'f0000000-0000-0000-0000-000000000002',
      tenantId: mockTenantId,
      hsCode: '8471.30.10',
      description: 'Laptops import duty & VAT',
      importTaxRate: 10.0,
      vatRate: 10.0,
    },
  });

  // 2. Seed Base Freight Rates
  const existingSeaRates = await prisma.baseFreightRate.findFirst({
    where: { tenantId: mockTenantId, originCode: 'SGSIN', destinationCode: 'VNSGN' },
  });

  if (!existingSeaRates) {
    await prisma.baseFreightRate.createMany({
      data: [
        {
          id: 'f1000000-0000-0000-0000-000000000001',
          tenantId: mockTenantId,
          originCode: 'SGSIN',
          destinationCode: 'VNSGN',
          transportMode: 'SEA',
          cargoType: 'GENERAL',
          ratePerKg: 0.5,
          ratePerCbm: 25.0,
          flatFee: 150.0,
          currency: 'USD',
        },
        {
          id: 'f1000000-0000-0000-0000-000000000002',
          tenantId: mockTenantId,
          originCode: 'SIN',
          destinationCode: 'SGN',
          transportMode: 'AIR',
          cargoType: 'GENERAL',
          ratePerKg: 3.5,
          ratePerCbm: 50.0,
          flatFee: 50.0,
          currency: 'USD',
        },
      ],
    });
  }

  // 3. Seed Port Handling Fees
  const existingPortFees = await prisma.portHandlingFee.findFirst({
    where: { tenantId: mockTenantId, portCode: 'SGSIN' },
  });

  if (!existingPortFees) {
    await prisma.portHandlingFee.createMany({
      data: [
        {
          id: 'p0000000-0000-0000-0000-000000000001',
          tenantId: mockTenantId,
          portCode: 'SGSIN',
          feeCode: 'THC',
          feeName: 'Terminal Handling Charge',
          amount: 120.0,
          currency: 'USD',
        },
        {
          id: 'p0000000-0000-0000-0000-000000000002',
          tenantId: mockTenantId,
          portCode: 'SGSIN',
          feeCode: 'DOC',
          feeName: 'Documentation Fee',
          amount: 30.0,
          currency: 'USD',
        },
      ],
    });
  }

  console.log('Financial database seeding completed successfully.');
}

main()
  .catch((e) => {
    console.error(e);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });
