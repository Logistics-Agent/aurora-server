import { Injectable } from '@nestjs/common';

/**
 * Domain Service: CostCalculatorDomainService
 * Contains pure domain mathematics and business logic for logistics pricing formulas.
 * Highly maintainable, zero dependencies on DB, 100% unit-testable.
 */
@Injectable()
export class CostCalculatorDomainService {
  /**
   * Calculates volumetric weight based on dimensions: (L * W * H) / VolumetricDivisor
   */
  calculateVolumetricWeight(
    lengthCm: number,
    widthCm: number,
    heightCm: number,
    divisor: number = 5000,
  ): number {
    if (lengthCm <= 0 || widthCm <= 0 || heightCm <= 0) {
      return 0;
    }
    const volWeight = (lengthCm * widthCm * heightCm) / divisor;
    return Number(volWeight.toFixed(2));
  }

  /**
   * Calculates Chargeable Weight as Max(GrossWeight, VolumetricWeight)
   */
  calculateChargeableWeight(
    grossWeightKg: number,
    volumetricWeightKg: number,
  ): number {
    return Number(Math.max(grossWeightKg || 0, volumetricWeightKg || 0).toFixed(2));
  }

  /**
   * Calculates Base Freight Fee: Max(ChargeableWeight * RatePerKg, VolumeCbm * RatePerCbm) + FlatFee
   */
  calculateFreightFee(
    chargeableWeightKg: number,
    volumeCbm: number,
    ratePerKg: number,
    ratePerCbm: number,
    flatFee: number = 0,
  ): number {
    const weightCost = (chargeableWeightKg || 0) * (ratePerKg || 0);
    const volumeCost = (volumeCbm || 0) * (ratePerCbm || 0);
    const totalFreight = Math.max(weightCost, volumeCost) + (flatFee || 0);

    return Number(totalFreight.toFixed(2));
  }

  /**
   * Calculates Import Duty and VAT:
   * Import Duty = Cargo Value * (Import Tax Rate / 100)
   * VAT Amount = (Cargo Value + Import Duty) * (VAT Rate / 100)
   */
  calculateCustomsDuty(
    cargoValue: number,
    importTaxRatePercent: number,
    vatRatePercent: number,
  ): {
    importDutyAmount: number;
    vatAmount: number;
    totalTaxAmount: number;
  } {
    const safeCargoValue = cargoValue > 0 ? cargoValue : 0;
    const importDutyAmount = safeCargoValue * ((importTaxRatePercent || 0) / 100.0);
    const vatAmount = (safeCargoValue + importDutyAmount) * ((vatRatePercent || 0) / 100.0);
    const totalTaxAmount = importDutyAmount + vatAmount;

    return {
      importDutyAmount: Number(importDutyAmount.toFixed(2)),
      vatAmount: Number(vatAmount.toFixed(2)),
      totalTaxAmount: Number(totalTaxAmount.toFixed(2)),
    };
  }

  /**
   * Calculates Minimum Acceptable Rate and Target Rate for Negotiation Agent
   * MinAcceptableRate = CostPrice * (1 + MinMarginPercent / 100)
   * TargetRate = CostPrice * (1 + TargetMarginPercent / 100)
   */
  calculateMinAcceptableRate(
    costPrice: number,
    minMarginPercent: number = 10,
    targetMarginPercent: number = 25,
  ): {
    minAcceptableRate: number;
    targetRate: number;
  } {
    const safeCost = costPrice > 0 ? costPrice : 0;
    const minAcceptableRate = safeCost * (1 + (minMarginPercent || 10) / 100.0);
    const targetRate = safeCost * (1 + (targetMarginPercent || 25) / 100.0);

    return {
      minAcceptableRate: Number(minAcceptableRate.toFixed(2)),
      targetRate: Number(targetRate.toFixed(2)),
    };
  }

  /**
   * Calculates Fuel Surcharge (FSC / Emergency Bunker Surcharge - EBS)
   * Phụ phí nhiên liệu biến động hàng tháng theo giá dầu thế giới.
   *
   * FSC Amount = Base Freight Cost * (FSC Rate Percent / 100)
   * EBS Amount = Base Freight Cost * (EBS Rate Percent / 100)
   *
   * @param baseFreightCost - Cước phí cơ bản đã tính (USD)
   * @param fscRatePercent  - Tỷ lệ Fuel Surcharge (%) - Thường 5-25% theo tháng
   * @param ebsRatePercent  - Tỷ lệ Emergency Bunker Surcharge (%) - Khi giá dầu bùng tăng đột biến
   */
  calculateFuelSurcharge(
    baseFreightCost: number,
    fscRatePercent: number = 10,
    ebsRatePercent: number = 0,
  ): {
    fscAmount: number;
    ebsAmount: number;
    totalSurcharge: number;
  } {
    const safeCost = baseFreightCost > 0 ? baseFreightCost : 0;
    const fscAmount = safeCost * ((fscRatePercent || 0) / 100.0);
    const ebsAmount = safeCost * ((ebsRatePercent || 0) / 100.0);
    const totalSurcharge = fscAmount + ebsAmount;

    return {
      fscAmount: Number(fscAmount.toFixed(2)),
      ebsAmount: Number(ebsAmount.toFixed(2)),
      totalSurcharge: Number(totalSurcharge.toFixed(2)),
    };
  }

  /**
   * Calculates Cargo Insurance Fee
   * Insurance Fee = Cargo Value * (Insurance Rate Percent / 100)
   * Thường dao động 0.1% - 0.5% giá trị lô hàng.
   */
  calculateCargoInsurance(
    cargoValue: number,
    insuranceRatePercent: number = 0.3,
  ): { insuranceFee: number } {
    const safeValue = cargoValue > 0 ? cargoValue : 0;
    const insuranceFee = safeValue * ((insuranceRatePercent || 0.3) / 100.0);
    return { insuranceFee: Number(insuranceFee.toFixed(2)) };
  }

  /**
   * TASK-001: Dynamic Margin Decay — Công thức từ Summary.md
   *
   * Min Acceptable Price = Base Cost × (1 + Base Margin% × (T_remaining / T_total)^γ)
   *
   * Khi T_remaining → 0 (gần cut-off): margin decay → 0, giá sàn tiệm cận cost price.
   * Khi T_remaining = T_total (vừa mở đàm phán): margin = full baseMarginPercent.
   */
  calculateDynamicMargin(
    costPrice: number,
    baseMarginPercent: number,
    remainingSeconds: number,
    totalSeconds: number,
    gamma: number = 2,
  ): {
    listPrice: number;
    minAcceptablePrice: number;
    currentMarginPercent: number;
    decayFactor: number;
  } {
    const safeCost = costPrice > 0 ? costPrice : 0;
    const safeTotal = totalSeconds > 0 ? totalSeconds : 1;
    const safeRemaining = Math.max(0, Math.min(remainingSeconds, safeTotal));
    const safeGamma = gamma >= 1 ? gamma : 2;

    const decayFactor = Math.pow(safeRemaining / safeTotal, safeGamma);
    const currentMarginPercent = (baseMarginPercent || 0) * decayFactor;
    const listPrice = safeCost * (1 + (baseMarginPercent || 0) / 100.0);
    const minAcceptablePrice = safeCost * (1 + currentMarginPercent / 100.0);

    return {
      listPrice: Number(listPrice.toFixed(2)),
      minAcceptablePrice: Number(minAcceptablePrice.toFixed(2)),
      currentMarginPercent: Number(currentMarginPercent.toFixed(4)),
      decayFactor: Number(decayFactor.toFixed(4)),
    };
  }
}

