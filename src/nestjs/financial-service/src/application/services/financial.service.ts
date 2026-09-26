import { Injectable, Logger, NotFoundException, BadRequestException } from '@nestjs/common';
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

  private requireTenant(tenantId?: string): string {
    if (!tenantId?.trim()) throw new BadRequestException('tenantId is required');
    return tenantId.trim();
  }

  async estimateCost(
    request: EstimateCostRequest,
    tenantId?: string,
  ): Promise<EstimateCostResponse> {
    const effectiveTenantId = this.requireTenant(tenantId || request.tenantId);
    if ((request.currency || 'USD').toUpperCase() !== 'USD') {
      throw new BadRequestException('Only USD estimates are supported until currency conversion is implemented');
    }
    if (!Number.isFinite(request.weightKg) || request.weightKg <= 0 ||
        !Number.isFinite(request.volumeCbm) || request.volumeCbm < 0) {
      throw new BadRequestException('Valid weightKg and volumeCbm are required');
    }
    if (!request.originPort?.trim() || !request.destinationPort?.trim()) {
      throw new BadRequestException('Origin and destination ports are required');
    }
    for (const [field, value] of Object.entries({
      cargoValue: request.cargoValue ?? 0,
      fscRatePercent: request.fscRatePercent ?? 0,
      ebsRatePercent: request.ebsRatePercent ?? 0,
      insuranceRatePercent: request.insuranceRatePercent ?? 0,
    })) {
      if (!Number.isFinite(value) || value < 0) {
        throw new BadRequestException(`${field} must be a non-negative finite number`);
      }
    }
    if (request.hsCodes && request.hsCodes.length > 1) {
      throw new BadRequestException('Multiple HS codes require item-level cargo values');
    }
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
      if (cachedRate.currency !== 'USD') throw new BadRequestException('Freight rate currency conversion is required');
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
        if (freightRate.currency !== 'USD') throw new BadRequestException('Freight rate currency conversion is required');
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
        throw new NotFoundException(`No freight rate configured for ${routeKey}`);
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
      throw new NotFoundException(`No port handling fee configured for ${request.originPort}`);
    }

    // ── 4. Customs Duties & VAT ─────────────────────────────────────────
    const cargoValue = request.cargoValue ?? 0;
    let totalImportDuty = 0;
    let totalVat = 0;
    const dutyDescriptions: string[] = [];

    if (request.hsCodes && request.hsCodes.length > 0) {
      if (!Number.isFinite(cargoValue) || cargoValue <= 0) {
        throw new BadRequestException('cargoValue is required when HS codes are supplied');
      }
      for (const hsCode of request.hsCodes) {
        const dutyRate = await this.prisma.customsDutyRate.findUnique({
          where: {
            tenantId_hsCode: {
              tenantId: effectiveTenantId,
              hsCode: hsCode,
            },
          },
        });

        if (!dutyRate) throw new NotFoundException(`No customs duty rate configured for HS ${hsCode}`);
        const importTaxRate = dutyRate.importTaxRate;
        const vatRate = dutyRate.vatRate;

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
      dutyDescriptions.push('Customs duty excluded: no HS code supplied');
    }

    const totalCustomsFee = Number((totalImportDuty + totalVat).toFixed(2));

    // ── 5. Fuel Surcharge (FSC) & Emergency Bunker Surcharge (EBS) ─────────
    // Phụ phí nhiên liệu biến động theo tháng, bắt buộc có trong logistics biển/hàng không
    const fscRatePercent = request.fscRatePercent !== undefined ? request.fscRatePercent : 0.0;
    const ebsRatePercent = request.ebsRatePercent !== undefined ? request.ebsRatePercent : 0.0;

    const surchargeResult = this.calculator.calculateFuelSurcharge(
      baseFreightCost,
      fscRatePercent,
      ebsRatePercent,
    );
    const fuelSurchargeFee = surchargeResult.totalSurcharge;

    // ── 6. Cargo Insurance Fee ───────────────────────────────────────────────
    // Phí bảo hiểm hàng hóa (mặc định 0.3% giá trị lô hàng)
    const insuranceRatePercent = request.insuranceRatePercent !== undefined ? request.insuranceRatePercent : 0;
    const { insuranceFee: cargoInsuranceFee } = this.calculator.calculateCargoInsurance(
      cargoValue || 0,
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
      currency: 'USD',
      calculationMethod,
      description: `Method: ${calculationMethod}. Port: $${portHandlingFees}. FSC: $${surchargeResult.fscAmount} (${fscRatePercent}%), EBS: $${surchargeResult.ebsAmount} (${ebsRatePercent}%). Insurance: $${cargoInsuranceFee} (${insuranceRatePercent}%). Customs: ${dutyDescriptions.join('; ')}`,
    };
  }

  async getCustomsDuty(
    request: GetCustomsDutyRequest,
    tenantId?: string,
  ): Promise<GetCustomsDutyResponse> {
    const effectiveTenantId = this.requireTenant(tenantId || request.tenantId);
    const dutyRate = await this.prisma.customsDutyRate.findUnique({
      where: {
        tenantId_hsCode: {
          tenantId: effectiveTenantId,
          hsCode: request.hsCode,
        },
      },
    });

    if (!dutyRate) throw new NotFoundException(`No customs duty rate configured for HS ${request.hsCode}`);
    if (!Number.isFinite(request.cargoValue) || request.cargoValue <= 0) {
      throw new BadRequestException('cargoValue must be positive');
    }
    const importTaxRate = dutyRate.importTaxRate;
    const vatRate = dutyRate.vatRate;
    const cargoValue = request.cargoValue!;

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
    const effectiveTenantId = this.requireTenant(tenantId || request.tenantId);

    if (!Number.isFinite(request.costPrice) || request.costPrice <= 0) {
      throw new BadRequestException('costPrice must be greater than 0');
    }
    if (!Number.isFinite(request.totalSeconds) || request.totalSeconds <= 0 ||
        !Number.isFinite(request.remainingSeconds) || request.remainingSeconds < 0 ||
        request.remainingSeconds > request.totalSeconds ||
        !Number.isFinite(request.baseMarginPercent) || request.baseMarginPercent < 0 ||
        (request.gamma !== undefined && (!Number.isFinite(request.gamma) || request.gamma <= 0))) {
      throw new BadRequestException('Invalid dynamic margin parameters');
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
    const effectiveTenantId = this.requireTenant(tenantId || request.tenantId);

    if (!request.fromCurrency || !request.toCurrency) {
      throw new BadRequestException('Currency pair is required');
    }
    // Tìm ngày target (mặc định hôm nay)
    const targetDate = request.date ? new Date(request.date) : new Date();
    if (Number.isNaN(targetDate.getTime())) throw new BadRequestException('Invalid exchange rate date');
    targetDate.setUTCHours(0, 0, 0, 0);

    const oldestAllowed = new Date(targetDate.getTime() - 7 * 86400000);
    const rate = await this.prisma.exchangeRate.findFirst({
      where: {
        tenantId: effectiveTenantId,
        fromCurrency: request.fromCurrency.toUpperCase(),
        toCurrency: request.toCurrency.toUpperCase(),
        validDate: { gte: oldestAllowed, lte: targetDate },
        source: { notIn: ['MOCK', 'FALLBACK'] },
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

    throw new NotFoundException(`No verified exchange rate for ${request.fromCurrency}/${request.toCurrency} within seven days`);
  }
}
