import { describe, it } from 'node:test';
import * as assert from 'node:assert/strict';
import { BadRequestException, ConflictException } from '@nestjs/common';
import { FinancialService } from './financial.service';
import { CostCalculatorDomainService } from '../../domain/services/cost-calculator.domain-service';

describe('FinancialService', () => {
  const createService = (overrides?: {
    cachedRate?: any;
    env?: string;
    portFees?: Array<{ amount: number; currency?: string }>;
    onFindManyDutyRates?: () => void;
  }) => {
    const calculator = new CostCalculatorDomainService();
    const dutyRateRecord = {
      hsCode: '8517',
      importTaxRate: 5,
      vatRate: 10,
      description: 'Telecom equipment',
    };
    const prismaMock: any = {
      baseFreightRate: {
        findFirst: async () => ({
          id: 'rate-1',
          ratePerKg: 2.0,
          ratePerCbm: 300,
          flatFee: 50,
          currency: 'USD',
        }),
      },
      portHandlingFee: {
        findMany: async () =>
          overrides?.portFees ?? [
            { amount: 120, currency: 'USD' },
            { amount: 80, currency: 'USD' },
          ],
      },
      customsDutyRate: {
        findUnique: async () => dutyRateRecord,
        findMany: async () => {
          overrides?.onFindManyDutyRates?.();
          return [dutyRateRecord];
        },
      },
      exchangeRate: {
        findFirst: async () => ({
          fromCurrency: 'USD',
          toCurrency: 'VND',
          rate: 25450,
          validDate: new Date('2026-11-14T00:00:00.000Z'),
          source: 'MOCK',
        }),
      },
      commercialQuote: {
        create: async ({ data }: any) => ({
          id: 'quote-1',
          createdAt: new Date('2026-11-20T00:00:00.000Z'),
          updatedAt: new Date('2026-11-20T00:00:00.000Z'),
          ...data,
        }),
        findMany: async () => [],
        findFirst: async () => null,
        update: async ({ where, data }: any) => ({
          id: where.id,
          createdAt: new Date('2026-11-20T00:00:00.000Z'),
          updatedAt: new Date('2026-11-20T00:00:00.000Z'),
          ...data,
        }),
      },
    };
    const configMock: any = {
      get: (key: string, defaultVal?: any) => {
        if (key === 'environment') return overrides?.env || 'development';
        return defaultVal;
      },
    };
    const rateCacheMock: any = {
      getRate: async () => overrides?.cachedRate ?? null,
      setRate: async () => undefined,
    };

    return new FinancialService(prismaMock, calculator, configMock, rateCacheMock);
  };

  it('estimates multimodal shipment cost with customs duty and port fees', async () => {
    const service = createService();
    const res = await service.estimateCost(
      {
        tenantId: 'tenant-1',
        originCountry: 'VN',
        originPort: 'VNSGN',
        destinationCountry: 'SG',
        destinationPort: 'SGSIN',
        weightKg: 1000,
        volumeCbm: 2,
        transportMode: 'SEA',
        cargoType: 'GENERAL',
        cargoValue: 10000,
        hsCodes: ['8517'],
        fscRatePercent: 10,
        ebsRatePercent: 0,
        insuranceRatePercent: 0.3,
      },
      'tenant-1',
    );

    assert.equal(res.chargeableWeightKg, 2000);
    assert.equal(res.baseFreightCost, 4050);
    assert.equal(res.portHandlingFees, 200);
    assert.equal(res.importDutyFee, 500);
    assert.equal(res.vatFee, 1050);
    assert.equal(res.currency, 'USD');
  });

  it('returns exchange rate in development mode and blocks unverified MOCK in production', async () => {
    const devService = createService({ env: 'development' });
    const fx = await devService.getExchangeRate({
      tenantId: 'tenant-1',
      fromCurrency: 'USD',
      toCurrency: 'VND',
    });
    assert.equal(fx.rate, 25450);
  });

  it('returns authoritative shipment quote via getShipmentQuote for HTTP contract', async () => {
    const service = createService();
    const quote = await service.getShipmentQuote('SHP-001', 'tenant-1', {
      originPort: 'VNSGN',
      destinationPort: 'SGSIN',
      weightKg: 1000,
      volumeCbm: 2,
    });
    assert.equal(quote.shipmentId, 'SHP-001');
    assert.equal(quote.currency, 'USD');
    assert.ok(quote.costPrice > 0);
    assert.ok(quote.bottomPrice > quote.costPrice);
    assert.ok(quote.listPrice > quote.bottomPrice);
  });

  it('rejects non-USD port handling fees and queries customs duty rates in batch', async () => {
    let findManyCalls = 0;
    const batchService = createService({
      onFindManyDutyRates: () => {
        findManyCalls++;
      },
    });
    await batchService.estimateCost(
      {
        tenantId: 'tenant-1',
        originCountry: 'VN',
        originPort: 'VNSGN',
        destinationCountry: 'SG',
        destinationPort: 'SGSIN',
        weightKg: 1000,
        volumeCbm: 2,
        transportMode: 'SEA',
        cargoType: 'GENERAL',
        cargoValue: 10000,
        hsCodes: ['8517'],
      },
      'tenant-1',
    );
    assert.equal(findManyCalls, 1);

    const nonUsdFeeService = createService({
      portFees: [{ amount: 500000, currency: 'VND' }],
    });
    await assert.rejects(
      () =>
        nonUsdFeeService.estimateCost(
          {
            tenantId: 'tenant-1',
            originCountry: 'VN',
            originPort: 'VNSGN',
            destinationCountry: 'SG',
            destinationPort: 'SGSIN',
            weightKg: 1000,
            volumeCbm: 2,
            transportMode: 'SEA',
            cargoType: 'GENERAL',
          },
          'tenant-1',
        ),
      BadRequestException,
    );
  });

  it('persists commercial quotes without shipmentId and links or conflicts on linkQuoteToShipment', async () => {
    const storedQuotes: any[] = [];
    const calculator = new CostCalculatorDomainService();
    const prismaWithQuotes: any = {
      baseFreightRate: {
        findFirst: async () => ({
          id: 'rate-1',
          ratePerKg: 2.0,
          ratePerCbm: 300,
          flatFee: 50,
          currency: 'USD',
        }),
      },
      portHandlingFee: {
        findMany: async () => [{ amount: 150, currency: 'USD' }],
      },
      customsDutyRate: { findMany: async () => [] },
      commercialQuote: {
        create: async ({ data }: any) => {
          const row = {
            id: `quote-${storedQuotes.length + 1}`,
            createdAt: new Date('2026-11-20T00:00:00.000Z'),
            updatedAt: new Date('2026-11-20T00:00:00.000Z'),
            ...data,
          };
          storedQuotes.push(row);
          return row;
        },
        findMany: async ({ where }: any) =>
          storedQuotes.filter(
            (q) =>
              q.tenantId === where.tenantId &&
              (!where.sourceThreadId || q.sourceThreadId === where.sourceThreadId) &&
              (!where.shipmentId || q.shipmentId === where.shipmentId) &&
              (!where.id || q.id === where.id),
          ),
        findFirst: async ({ where }: any) =>
          storedQuotes.find(
            (q) =>
              q.tenantId === where.tenantId &&
              (!where.id || q.id === where.id) &&
              (!where.shipmentId || q.shipmentId === where.shipmentId),
          ) ?? null,
        update: async ({ where, data }: any) => {
          const target = storedQuotes.find((q) => q.id === where.id);
          Object.assign(target, data, { updatedAt: new Date('2026-11-20T01:00:00.000Z') });
          return target;
        },
      },
    };
    const svc = new FinancialService(
      prismaWithQuotes,
      calculator,
      { get: () => 'development' } as any,
      { getRate: async () => null, setRate: async () => undefined } as any,
    );

    const q1 = await svc.createQuote(
      {
        tenantId: 'tenant-1',
        sourceThreadId: 'thread-abc',
        originCountry: 'VN',
        originPort: 'VNSGN',
        destinationCountry: 'SG',
        destinationPort: 'SGSIN',
        weightKg: 1000,
        volumeCbm: 2,
      },
      'tenant-1',
    );
    const q2 = await svc.createQuote(
      {
        tenantId: 'tenant-1',
        sourceThreadId: 'thread-abc',
        originCountry: 'VN',
        originPort: 'VNSGN',
        destinationCountry: 'SG',
        destinationPort: 'SGSIN',
        weightKg: 2000,
        volumeCbm: 4,
      },
      'tenant-1',
    );

    assert.equal(q1.shipmentId, '');
    assert.equal(q1.status, 'QUOTED');
    assert.ok(q1.minAcceptableRate > q1.costPrice);
    assert.ok(q1.targetRate > q1.minAcceptableRate);

    const byThread = await svc.listQuotes({ sourceThreadId: 'thread-abc' }, 'tenant-1');
    assert.equal(byThread.quotes.length, 2);

    const linked = await svc.linkQuoteToShipment({ quoteId: q1.id, shipmentId: 'SHP-901' }, 'tenant-1');
    assert.equal(linked.shipmentId, 'SHP-901');
    assert.equal(linked.status, 'LINKED');

    const byShipment = await svc.getShipmentQuote('SHP-901', 'tenant-1');
    assert.equal(byShipment.listPrice, q1.targetRate);
    assert.equal(byShipment.bottomPrice, q1.minAcceptableRate);

    await assert.rejects(
      () => svc.linkQuoteToShipment({ quoteId: q1.id, shipmentId: 'SHP-OTHER' }, 'tenant-1'),
      ConflictException,
    );
    assert.notEqual(q2.id, q1.id);
  });
});
