import { Injectable, Logger, NotFoundException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { PrismaService } from '../../infrastructure/prisma/prisma.service';
import { CostCalculatorDomainService } from '../../domain/services/cost-calculator.domain-service';
import {
  EstimateCostRequest,
  EstimateCostResponse,
  GetCustomsDutyRequest,
  GetCustomsDutyResponse,
  GetMinAcceptableRateRequest,
  GetMinAcceptableRateResponse,
  GetDynamicMarginRequest,
  GetDynamicMarginResponse,
  GetExchangeRateRequest,
  GetExchangeRateResponse,
} from '../../interface/dto/financial.dto';

import { RateCacheService } from '../../infrastructure/cache/rate-cache.service';

@Injectable()
export class FinancialService {
  private readonly logger = new Logger(FinancialService.name);

  constructor(
    private readonly prisma: PrismaService,
    private readonly calculator: CostCalculatorDomainService,
    private readonly configService: ConfigService,
    private readonly rateCache: RateCacheService,
  ) {}

  async estimateCost(
    request: EstimateCostRequest,
    tenantId?: string,
  ): Promise<EstimateCostResponse> {
    const effectiveTenantId = tenantId || request.tenantId || 'a0000000-0000-0000-0000-000000000001';
    const mode = (request.transportMode || 'SEA').toUpperCase();
    const cargoType = request.cargoType || 'GENERAL';

    // ── 1. Volumetric & Chargeable Weight ────────────────────────────────
    const divisor =
      mode === 'AIR'
        ? this.configService.get<number>('logistics.volumetricDivisorAir', 5000)
        : this.configService.get<number>('logistics.volumetricDivisorSea', 6000);

    let volumetricWeightKg = 0;
    if (request.lengthCm && request.widthCm && request.heightCm) {
      volumetricWeightKg = this.calculator.calculateVolumetricWeight(
        request.lengthCm,
        request.widthCm,
        request.heightCm,
        divisor,
      );
    } else {
      // Estimated volumetric weight from CBM if dimensions not provided (1 CBM = 167kg Air, 1000kg Sea)
      const cbmRatio = mode === 'AIR' ? 167 : 1000;
      volumetricWeightKg = Number(((request.volumeCbm || 0) * cbmRatio).toFixed(2));
    }

    const chargeableWeightKg = this.calculator.calculateChargeableWeight(
      request.weightKg || 0,
      volumetricWeightKg,
    );

    // ── 2. Base Freight Calculation (TASK-012 Redis Cache < 2ms) ───────────
    let baseFreightCost = 0;
    let calculationMethod = 'STANDARD_DYNAMIC_RATE';
    const routeKey = `${request.originPort}_${request.destinationPort}_${mode}_${cargoType}`;

    const cachedRate = await this.rateCache.getRate(effectiveTenantId, routeKey);

    if (cachedRate) {
      baseFreightCost = this.calculator.calculateFreightFee(
        chargeableWeightKg,
        request.volumeCbm || 0,
        cachedRate.ratePerKg,
        cachedRate.ratePerCbm,
        cachedRate.flatFee,
      );
      calculationMethod = `REDIS_CACHE_${routeKey}`;
    } else {
      const freightRate = await this.prisma.baseFreightRate.findFirst({
        where: {
          tenantId: effectiveTenantId,
          originCode: request.originPort,
          destinationCode: request.destinationPort,
          transportMode: mode,
          cargoType: cargoType,
        },
      });

      if (freightRate) {
        baseFreightCost = this.calculator.calculateFreightFee(
          chargeableWeightKg,
          request.volumeCbm || 0,
          freightRate.ratePerKg,
          freightRate.ratePerCbm,
          freightRate.flatFee,
        );
        calculationMethod = `DYNAMIC_RATE_ID_${freightRate.id}`;

        // Cache for future sub-2ms calls
        await this.rateCache.setRate(effectiveTenantId, routeKey, {
          ratePerKg: freightRate.ratePerKg,
          ratePerCbm: freightRate.ratePerCbm,
          flatFee: freightRate.flatFee,
          currency: freightRate.currency,
        });
      } else {
        // Dynamic fallback based on mode
        const ratePerKg = mode === 'AIR' ? 3.5 : 0.8;
        const ratePerCbm = mode === 'AIR' ? 50.0 : 35.0;
        const flatFee = mode === 'AIR' ? 50.0 : 150.0;

        baseFreightCost = this.calculator.calculateFreightFee(
          chargeableWeightKg,
          request.volumeCbm || 0,
          ratePerKg,
          ratePerCbm,
          flatFee,
        );
        calculationMethod = `FALLBACK_${mode}_FORMULA`;
      }
    }

    // ── 3. Port Handling Fees ──────────────────────────────────────────
    const portFees = await this.prisma.portHandlingFee.findMany({
      where: {
        tenantId: effectiveTenantId,
        portCode: request.originPort,
      },
    });

    let portHandlingFees = 0;
    if (portFees.length > 0) {
      portHandlingFees = portFees.reduce((sum, fee) => sum + fee.amount, 0);
    } else {
      // Default port fee structure ($120 base + $10/CBM)
      portHandlingFees = Number((120.0 + (request.volumeCbm || 0) * 10.0).toFixed(2));
    }

    // ── 4. Customs Duties & VAT ─────────────────────────────────────────
    const cargoValue = request.cargoValue || request.weightKg * 2.5; // default valuation if unstated
    let totalImportDuty = 0;
    let totalVat = 0;
    const dutyDescriptions: string[] = [];

    const defaultVatRate = this.configService.get<number>('logistics.defaultVatRate', 10.0);

    if (request.hsCodes && request.hsCodes.length > 0) {
      for (const hsCode of request.hsCodes) {
        const dutyRate = await this.prisma.customsDutyRate.findUnique({
          where: {
            tenantId_hsCode: {
              tenantId: effectiveTenantId,
              hsCode: hsCode,
            },
          },
        });

        const importTaxRate = dutyRate ? dutyRate.importTaxRate : 5.0;
        const vatRate = dutyRate ? dutyRate.vatRate : defaultVatRate;

        const dutyRes = this.calculator.calculateCustomsDuty(
          cargoValue,
          importTaxRate,
          vatRate,
        );

        totalImportDuty += dutyRes.importDutyAmount;
        totalVat += dutyRes.vatAmount;
        dutyDescriptions.push(
          `HS ${hsCode}: Import Duty ${importTaxRate}% ($${dutyRes.importDutyAmount}), VAT ${vatRate}% ($${dutyRes.vatAmount})`,
        );
      }
    } else {
      const dutyRes = this.calculator.calculateCustomsDuty(cargoValue, 2.0, defaultVatRate);
      totalImportDuty = dutyRes.importDutyAmount;
      totalVat = dutyRes.vatAmount;
      dutyDescriptions.push(
        `Default Duty 2% ($${dutyRes.importDutyAmount}), VAT ${defaultVatRate}% ($${dutyRes.vatAmount})`,
      );
    }

    const totalCustomsFee = Number((totalImportDuty + totalVat).toFixed(2));

    // ── 5. Fuel Surcharge (FSC) & Emergency Bunker Surcharge (EBS) ─────────
    // Phụ phí nhiên liệu biến động theo tháng, bắt buộc có trong logistics biển/hàng không
    const fscRatePercent = request.fscRatePercent !== undefined ? request.fscRatePercent : 10.0;
    const ebsRatePercent = request.ebsRatePercent !== undefined ? request.ebsRatePercent : 0.0;

    const surchargeResult = this.calculator.calculateFuelSurcharge(
      baseFreightCost,
      fscRatePercent,
      ebsRatePercent,
    );
    const fuelSurchargeFee = surchargeResult.totalSurcharge;

    // ── 6. Cargo Insurance Fee ───────────────────────────────────────────────
    // Phí bảo hiểm hàng hóa (mặc định 0.3% giá trị lô hàng)
    const insuranceRatePercent = request.insuranceRatePercent !== undefined ? request.insuranceRatePercent : 0.3;
    const { insuranceFee: cargoInsuranceFee } = this.calculator.calculateCargoInsurance(
      cargoValue,
      insuranceRatePercent,
    );

    const totalEstimatedCost = Number(
      (baseFreightCost + portHandlingFees + fuelSurchargeFee + cargoInsuranceFee + totalCustomsFee).toFixed(2),
    );

    return {
      baseFreightCost,
      portHandlingFees,
      fuelSurchargeFee,
      cargoInsuranceFee,
      importDutyFee: Number(totalImportDuty.toFixed(2)),
      vatFee: Number(totalVat.toFixed(2)),
      totalCustomsFee,
      totalEstimatedCost,
      chargeableWeightKg,
      volumetricWeightKg,
      currency: request.currency || 'USD',
      calculationMethod,
      description: `Method: ${calculationMethod}. Port: $${portHandlingFees}. FSC: $${surchargeResult.fscAmount} (${fscRatePercent}%), EBS: $${surchargeResult.ebsAmount} (${ebsRatePercent}%). Insurance: $${cargoInsuranceFee} (${insuranceRatePercent}%). Customs: ${dutyDescriptions.join('; ')}`,
    };
  }

  async getCustomsDuty(
    request: GetCustomsDutyRequest,
    tenantId?: string,
  ): Promise<GetCustomsDutyResponse> {
    const effectiveTenantId = tenantId || request.tenantId || 'a0000000-0000-0000-0000-000000000001';
    const defaultVatRate = this.configService.get<number>('logistics.defaultVatRate', 10.0);

    const dutyRate = await this.prisma.customsDutyRate.findUnique({
      where: {
        tenantId_hsCode: {
          tenantId: effectiveTenantId,
          hsCode: request.hsCode,
        },
      },
    });

    const importTaxRate = dutyRate ? dutyRate.importTaxRate : 5.0;
    const vatRate = dutyRate ? dutyRate.vatRate : defaultVatRate;
    const cargoValue = request.cargoValue || 1000.0;

    const dutyRes = this.calculator.calculateCustomsDuty(cargoValue, importTaxRate, vatRate);

    return {
      hsCode: request.hsCode,
      importTaxRate,
      vatRate,
      importDutyAmount: dutyRes.importDutyAmount,
      vatAmount: dutyRes.vatAmount,
      totalTaxAmount: dutyRes.totalTaxAmount,
      description: dutyRate?.description || `Customs duty rates for HS Code ${request.hsCode}`,
    };
  }

  async getMinAcceptableRate(
    request: GetMinAcceptableRateRequest,
    tenantId?: string,
  ): Promise<GetMinAcceptableRateResponse> {
    const costEstimate = await this.estimateCost(
      {
        originCountry: '',
        originPort: request.originPort,
        destinationCountry: '',
        destinationPort: request.destinationPort,
        weightKg: request.weightKg,
        volumeCbm: request.volumeCbm,
        transportMode: request.transportMode,
        cargoType: request.cargoType,
      },
      tenantId,
    );

    const costPrice = costEstimate.totalEstimatedCost;
    const rates = this.calculator.calculateMinAcceptableRate(
      costPrice,
      request.minMarginPercent || 10.0,
      request.targetMarginPercent || 25.0,
    );

    return {
      costPrice,
      minAcceptableRate: rates.minAcceptableRate,
      targetRate: rates.targetRate,
      currency: costEstimate.currency,
      breakdownNote: `Cost Price: $${costPrice}. Minimum Acceptable Rate (+${request.minMarginPercent || 10}%): $${rates.minAcceptableRate}. Target Rate (+${request.targetMarginPercent || 25}%): $${rates.targetRate}.`,
    };
  }

  // ── TASK-001: Dynamic Margin Decay ────────────────────────────────────────────

  async getDynamicMargin(
    request: GetDynamicMarginRequest,
    tenantId?: string,
  ): Promise<GetDynamicMarginResponse> {
    const effectiveTenantId = tenantId || request.tenantId || 'a0000000-0000-0000-0000-000000000001';

    if (request.costPrice <= 0) {
      throw new Error('costPrice must be greater than 0');
    }
    if (request.totalSeconds <= 0) {
      throw new Error('totalSeconds must be greater than 0');
    }

    const result = this.calculator.calculateDynamicMargin(
      request.costPrice,
      request.baseMarginPercent,
      request.remainingSeconds,
      request.totalSeconds,
      request.gamma || 2,
    );

    this.logger.debug(
      `[DynamicMargin] Shipment ${request.shipmentId} | Remaining: ${request.remainingSeconds}s / ${request.totalSeconds}s | DecayFactor: ${result.decayFactor} | MinPrice: $${result.minAcceptablePrice}`,
    );

    return {
      costPrice: request.costPrice,
      listPrice: result.listPrice,
      minAcceptablePrice: result.minAcceptablePrice,
      currentMarginPercent: result.currentMarginPercent,
      decayFactor: result.decayFactor,
      currency: 'USD',
      note: `Shipment ${request.shipmentId} | BaseMargin: ${request.baseMarginPercent}% | DecayFactor: ${result.decayFactor} (gamma=${request.gamma || 2}) | Current margin: ${result.currentMarginPercent.toFixed(2)}%`,
    };
  }

  // ── TASK-002: Exchange Rate Engine ────────────────────────────────────────────

  async getExchangeRate(
    request: GetExchangeRateRequest,
    tenantId?: string,
  ): Promise<GetExchangeRateResponse> {
    const effectiveTenantId = tenantId || request.tenantId || 'a0000000-0000-0000-0000-000000000001';

    // Tìm ngày target (mặc định hôm nay)
    const targetDate = request.date ? new Date(request.date) : new Date();
    targetDate.setUTCHours(0, 0, 0, 0);

    // Tìm tỷ giá mới nhất không quá 7 ngày cũ
    const rate = await this.prisma.exchangeRate.findFirst({
      where: {
        tenantId: effectiveTenantId,
        fromCurrency: request.fromCurrency.toUpperCase(),
        toCurrency: request.toCurrency.toUpperCase(),
        validDate: { lte: targetDate },
      },
      orderBy: { validDate: 'desc' },
    });

    if (rate) {
      return {
        fromCurrency: rate.fromCurrency,
        toCurrency: rate.toCurrency,
        rate: rate.rate,
        validDate: rate.validDate.toISOString(),
        source: rate.source,
      };
    }

    // Fallback hardcoded nếu chưa có dữ liệu DB (chạy lần đầu trước cron)
    const FALLBACK_RATES: Record<string, number> = {
      'USD_VND': 25450.0,
      'EUR_VND': 27800.0,
      'USD_EUR': 0.915,
      'EUR_USD': 1.093,
    };
    const key = `${request.fromCurrency.toUpperCase()}_${request.toCurrency.toUpperCase()}`;
    const fallbackRate = FALLBACK_RATES[key] || 1.0;

    this.logger.warn(
      `[ExchangeRate] No DB rate found for ${key}. Using fallback: ${fallbackRate}. Run ExchangeRateSyncCronJob to populate.`,
    );

    return {
      fromCurrency: request.fromCurrency.toUpperCase(),
      toCurrency: request.toCurrency.toUpperCase(),
      rate: fallbackRate,
      validDate: targetDate.toISOString(),
      source: 'FALLBACK',
    };
  }
}
