import { Injectable } from '@nestjs/common';

export const DEFAULT_MIN_MARGIN_PERCENT = 10;
export const DEFAULT_TARGET_MARGIN_PERCENT = 25;

export interface CorridorRatePolicy {
  ratePerKm: number;
  ratePerTonKm: number;
  handlingFeeUsd: number;
  fuelSurchargePercent: number;
}

export const CORRIDOR_RATES_BY_MODE: Record<string, CorridorRatePolicy> = {
  ROAD: { ratePerKm: 1.15, ratePerTonKm: 0.085, handlingFeeUsd: 140, fuelSurchargePercent: 12 },
  SEA: { ratePerKm: 0.45, ratePerTonKm: 0.032, handlingFeeUsd: 180, fuelSurchargePercent: 10 },
  OCEAN: { ratePerKm: 0.45, ratePerTonKm: 0.032, handlingFeeUsd: 180, fuelSurchargePercent: 10 },
  AIR: { ratePerKm: 2.2, ratePerTonKm: 0.24, handlingFeeUsd: 160, fuelSurchargePercent: 15 },
  MULTIMODAL: { ratePerKm: 0.95, ratePerTonKm: 0.065, handlingFeeUsd: 170, fuelSurchargePercent: 11 },
};

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
    const insuranceFee = safeValue * ((insuranceRatePercent ?? 0.3) / 100.0);
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
    const safeGamma = gamma > 0 ? gamma : 2;

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

  /**
   * Computes real route-corridor cost from Route Planning distance, weight, volume, and transport mode.
   */
  calculateRouteCorridorCost(input: {
    distanceKm: number;
    weightKg: number;
    volumeCbm: number;
    transportMode?: string;
  }): {
    baseFreightCost: number;
    portHandlingFees: number;
    fuelSurchargeFee: number;
    totalEstimatedCost: number;
    calculationMethod: string;
    description: string;
  } {
    const mode = (input.transportMode || 'ROAD').toUpperCase();
    const policy = CORRIDOR_RATES_BY_MODE[mode] ?? CORRIDOR_RATES_BY_MODE.ROAD;
    const distance = Math.max(1, Number(input.distanceKm) || 0);
    const weightTons = Math.max(0.5, (Number(input.weightKg) || 0) / 1000);
    const volumetricRatio = mode === 'AIR' ? 167 : 1000;
    const chargeableTons = Math.max(weightTons, ((Number(input.volumeCbm) || 0) * volumetricRatio) / 1000);

    const distanceCharge = distance * policy.ratePerKm;
    const weightDistanceCharge = distance * chargeableTons * policy.ratePerTonKm;
    const baseFreightCost = Number((distanceCharge + weightDistanceCharge).toFixed(2));
    const portHandlingFees = policy.handlingFeeUsd;
    const fuelSurchargeFee = Number(((baseFreightCost * policy.fuelSurchargePercent) / 100).toFixed(2));
    const totalEstimatedCost = Number((baseFreightCost + portHandlingFees + fuelSurchargeFee).toFixed(2));
    const calculationMethod = `ROUTE_CORRIDOR_${mode}`;

    return {
      baseFreightCost,
      portHandlingFees,
      fuelSurchargeFee,
      totalEstimatedCost,
      calculationMethod,
      description: `${calculationMethod}: ${distance.toFixed(1)} km · ${chargeableTons.toFixed(2)}t chargeable · Base $${baseFreightCost} + Handling $${portHandlingFees} + FSC $${fuelSurchargeFee}`,
    };
  }
}

