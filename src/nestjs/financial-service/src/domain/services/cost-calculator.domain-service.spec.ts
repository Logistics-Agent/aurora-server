import { describe, it } from 'node:test';
import * as assert from 'node:assert/strict';
import { CostCalculatorDomainService } from './cost-calculator.domain-service';

describe('CostCalculatorDomainService', () => {
  const calculator = new CostCalculatorDomainService();

  it('calculates volumetric weight and chargeable weight accurately', () => {
    const volAir = calculator.calculateVolumetricWeight(100, 100, 100, 5000);
    assert.equal(volAir, 200);

    const chargeable = calculator.calculateChargeableWeight(150, volAir);
    assert.equal(chargeable, 200);
  });

  it('calculates base freight fee using max(weightCost, volumeCost) + flatFee', () => {
    const fee = calculator.calculateFreightFee(2000, 5, 1.5, 500, 100);
    // weightCost = 3000, volumeCost = 2500 -> max = 3000 + 100 = 3100
    assert.equal(fee, 3100);
  });

  it('calculates import duty and compound VAT accurately', () => {
    const res = calculator.calculateCustomsDuty(20000, 10, 10);
    assert.equal(res.importDutyAmount, 2000);
    assert.equal(res.vatAmount, 2200);
    assert.equal(res.totalTaxAmount, 4200);
  });

  it('calculates fuel surcharge (FSC + EBS) and cargo insurance', () => {
    const surcharge = calculator.calculateFuelSurcharge(1000, 12, 3);
    assert.equal(surcharge.fscAmount, 120);
    assert.equal(surcharge.ebsAmount, 30);
    assert.equal(surcharge.totalSurcharge, 150);

    const ins = calculator.calculateCargoInsurance(50000, 0.3);
    assert.equal(ins.insuranceFee, 150);
  });

  it('calculates dynamic margin decay for gamma = 2, gamma = 1, and gamma = 0.5', () => {
    // gamma = 2 (fast decay): 50% time left -> decayFactor = 0.25 -> 5% margin -> $1050
    const g2 = calculator.calculateDynamicMargin(1000, 20, 50, 100, 2);
    assert.equal(g2.listPrice, 1200);
    assert.equal(g2.decayFactor, 0.25);
    assert.equal(g2.currentMarginPercent, 5);
    assert.equal(g2.minAcceptablePrice, 1050);

    // gamma = 1 (linear decay): 50% time left -> decayFactor = 0.5 -> 10% margin -> $1100
    const g1 = calculator.calculateDynamicMargin(1000, 20, 50, 100, 1);
    assert.equal(g1.decayFactor, 0.5);
    assert.equal(g1.minAcceptablePrice, 1100);

    // gamma = 0.5 (hold price until last minute): 25% time left -> sqrt(0.25) = 0.5 -> 10% margin -> $1100
    const g05 = calculator.calculateDynamicMargin(1000, 20, 25, 100, 0.5);
    assert.equal(g05.decayFactor, 0.5);
    assert.equal(g05.minAcceptablePrice, 1100);
  });
});
