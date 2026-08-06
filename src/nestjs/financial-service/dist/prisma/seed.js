"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const client_1 = require("@prisma/client");
const prisma = new client_1.PrismaClient();
async function main() {
    console.log('Seeding financial database...');
    await prisma.customsDutyRate.upsert({
        where: {
            originCountry_destinationCountry_hsCode: {
                originCountry: 'CN',
                destinationCountry: 'VN',
                hsCode: '8517.12.00',
            },
        },
        update: {},
        create: {
            id: 'f0000000-0000-0000-0000-000000000001',
            originCountry: 'CN',
            destinationCountry: 'VN',
            hsCode: '8517.12.00',
            dutyRatePercentage: 5.0,
            description: 'Mobile phones import duty',
        },
    });
    await prisma.customsDutyRate.upsert({
        where: {
            originCountry_destinationCountry_hsCode: {
                originCountry: 'US',
                destinationCountry: 'VN',
                hsCode: '8471.30.10',
            },
        },
        update: {},
        create: {
            id: 'f0000000-0000-0000-0000-000000000002',
            originCountry: 'US',
            destinationCountry: 'VN',
            hsCode: '8471.30.10',
            dutyRatePercentage: 10.0,
            description: 'Laptops import duty',
        },
    });
    const mockTenantId = 'a0000000-0000-0000-0000-000000000001';
    const existingRates = await prisma.baseFreightRate.findFirst({
        where: { tenantId: mockTenantId, originPort: 'SGSIN', destinationPort: 'VNSGN' },
    });
    if (!existingRates) {
        await prisma.baseFreightRate.createMany({
            data: [
                {
                    id: 'f1000000-0000-0000-0000-000000000001',
                    tenantId: mockTenantId,
                    originPort: 'SGSIN',
                    destinationPort: 'VNSGN',
                    cargoType: 'GENERAL',
                    ratePerKg: 0.5,
                    ratePerCbm: 25.0,
                    flatFee: 150.0,
                },
                {
                    id: 'f1000000-0000-0000-0000-000000000002',
                    tenantId: mockTenantId,
                    originPort: 'SGSIN',
                    destinationPort: 'VNSGN',
                    cargoType: 'REFRIGERATED',
                    ratePerKg: 1.2,
                    ratePerCbm: 60.0,
                    flatFee: 300.0,
                },
            ],
        });
    }
    console.log('Seeding completed successfully.');
}
main()
    .catch((e) => {
    console.error(e);
    process.exit(1);
})
    .finally(async () => {
    await prisma.$disconnect();
});
//# sourceMappingURL=seed.js.map