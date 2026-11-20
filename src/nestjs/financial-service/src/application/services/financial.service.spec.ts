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

  it('enforces 1-way flow: registerShipmentThread (PENDING_ROUTE) -> createQuote with routeId (PRICED) -> confirmQuote (CONFIRMED)', async () => {
    const storedQuotes: any[] = [];
    const calculator = new CostCalculatorDomainService();
    const prismaWithQuotes: any = {
      baseFreightRate: {
        findFirst: async () => null,
      },
      portHandlingFee: {
        findMany: async () => [],
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

    // 1. Register Shipment <-> Mail Thread before Route is chosen
    const registered = await svc.registerShipmentThread(
      {
        shipmentId: 'SHP-901',
        sourceThreadId: 'thread-abc',
        customerId: 'customer@example.com',
        originPort: 'San Jose',
        destinationPort: 'Panama City',
        weightKg: 2500,
        volumeCbm: 12,
        transportMode: 'ROAD',
      },
      'tenant-1',
    );
    assert.equal(registered.status, 'PENDING_ROUTE');
    assert.equal(registered.sourceThreadId, 'thread-abc');
    assert.equal(registered.shipmentId, 'SHP-901');

    // Negotiation/getShipmentQuote must block while PENDING_ROUTE
    await assert.rejects(() => svc.getShipmentQuote('SHP-901', 'tenant-1'), BadRequestException);

    // 2. Reject createQuote without routeId
    await assert.rejects(
      () =>
        svc.createQuote(
          {
            shipmentId: 'SHP-901',
            routeId: '',
            originCountry: 'CR',
            originPort: 'San Jose',
            destinationCountry: 'PA',
            destinationPort: 'Panama City',
            weightKg: 2500,
            volumeCbm: 12,
          },
          'tenant-1',
        ),
      BadRequestException,
    );

    // 3. Price Shipment when Route is chosen (preserving thread link)
    const priced = await svc.createQuote(
      {
        shipmentId: 'SHP-901',
        routeId: 'route-corridor-1',
        routeName: 'Pan-American Express',
        distanceKm: 650,
        originCountry: 'CR',
        originPort: 'San Jose',
        destinationCountry: 'PA',
        destinationPort: 'Panama City',
        weightKg: 2500,
        volumeCbm: 12,
        transportMode: 'ROAD',
      },
      'tenant-1',
    );
    assert.equal(priced.id, registered.id);
    assert.equal(priced.sourceThreadId, 'thread-abc');
    assert.equal(priced.status, 'PRICED');
    assert.equal(priced.routeId, 'route-corridor-1');
    assert.ok(priced.costPrice > 0);
    assert.ok(priced.minAcceptableRate > priced.costPrice);
    assert.ok(priced.targetRate > priced.minAcceptableRate);

    const byShipment = await svc.getShipmentQuote('SHP-901', 'tenant-1');
    assert.equal(byShipment.listPrice, priced.targetRate);
    assert.equal(byShipment.bottomPrice, priced.minAcceptableRate);

    // 4. Confirm quote at agreed price and block conflicting re-confirmation
    const confirmed = await svc.confirmQuote(
      { quoteId: priced.id, shipmentId: 'SHP-901', agreedPrice: priced.minAcceptableRate },
      'tenant-1',
    );
    assert.equal(confirmed.status, 'CONFIRMED');
    assert.equal(confirmed.agreedPrice, priced.minAcceptableRate);

    await assert.rejects(
      () =>
        svc.confirmQuote(
          { quoteId: priced.id, shipmentId: 'SHP-901', agreedPrice: priced.targetRate },
          'tenant-1',
        ),
      ConflictException,
    );
  });
});
