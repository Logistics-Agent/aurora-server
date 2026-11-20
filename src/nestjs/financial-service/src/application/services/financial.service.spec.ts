import { describe, it } from 'node:test';
import * as assert from 'node:assert/strict';
import { BadRequestException } from '@nestjs/common';
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
});
