import {
  Injectable,
  Logger,
  NotFoundException,
  BadRequestException,
  ConflictException,
} from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { PrismaService } from '../../infrastructure/prisma/prisma.service';
import {
  CostCalculatorDomainService,
  DEFAULT_MIN_MARGIN_PERCENT,
  DEFAULT_TARGET_MARGIN_PERCENT,
} from '../../domain/services/cost-calculator.domain-service';
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
  CommercialQuoteDto,
  CreateQuoteRequest,
  ListQuotesRequest,
  ListQuotesResponse,
  RegisterShipmentThreadRequest,
  ConfirmQuoteRequest,
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
      const nonUsdFee = portFees.find(
        (fee) => fee.currency && fee.currency.toUpperCase() !== 'USD',
      );
      if (nonUsdFee) {
        throw new BadRequestException('Port handling fee currency conversion is required');
      }
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
      const uniqueHsCodes = Array.from(new Set(request.hsCodes));
      const dutyRates =
        typeof this.prisma.customsDutyRate.findMany === 'function'
          ? await this.prisma.customsDutyRate.findMany({
              where: {
                tenantId: effectiveTenantId,
                hsCode: { in: uniqueHsCodes },
              },
            })
          : (
              await Promise.all(
                uniqueHsCodes.map((hsCode) =>
                  this.prisma.customsDutyRate.findUnique({
                    where: { tenantId_hsCode: { tenantId: effectiveTenantId, hsCode } },
                  }),
                ),
              )
            ).filter((rate): rate is NonNullable<typeof rate> => Boolean(rate));
      const dutyRateByCode = new Map(dutyRates.map((rate) => [rate.hsCode, rate]));

      for (const hsCode of request.hsCodes) {
        const dutyRate = dutyRateByCode.get(hsCode);
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
        ...(this.configService.get<string>('environment') === 'production' ? { source: { notIn: ['MOCK', 'FALLBACK'] } } : {}),
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

  private mapQuoteToDto(record: any): CommercialQuoteDto {
    return {
      id: record.id,
      tenantId: record.tenantId,
      sourceThreadId: record.sourceThreadId ?? '',
      shipmentId: record.shipmentId ?? '',
      customerId: record.customerId ?? '',
      routeId: record.routeId ?? '',
      routeName: record.routeName ?? '',
      distanceKm: record.distanceKm ?? 0,
      originCountry: record.originCountry ?? '',
      originPort: record.originPort,
      destinationCountry: record.destinationCountry ?? '',
      destinationPort: record.destinationPort,
      weightKg: record.weightKg,
      volumeCbm: record.volumeCbm ?? 0,
      transportMode: record.transportMode ?? 'SEA',
      cargoType: record.cargoType ?? 'GENERAL',
      cargoValue: record.cargoValue ?? 0,
      hsCodes: Array.isArray(record.hsCodes) ? record.hsCodes : [],
      baseFreightCost: record.baseFreightCost ?? 0,
      portHandlingFees: record.portHandlingFees ?? 0,
      fuelSurchargeFee: record.fuelSurchargeFee ?? 0,
      cargoInsuranceFee: record.cargoInsuranceFee ?? 0,
      totalCustomsFee: record.totalCustomsFee ?? 0,
      costPrice: record.costPrice ?? 0,
      minAcceptableRate: record.minAcceptableRate ?? 0,
      targetRate: record.targetRate ?? 0,
      agreedPrice: record.agreedPrice ?? 0,
      currency: record.currency ?? 'USD',
      calculationMethod: record.calculationMethod ?? '',
      description: record.description ?? '',
      status: record.status ?? 'PENDING_ROUTE',
      createdAt: record.createdAt instanceof Date ? record.createdAt.toISOString() : String(record.createdAt ?? ''),
      updatedAt: record.updatedAt instanceof Date ? record.updatedAt.toISOString() : String(record.updatedAt ?? ''),
    };
  }

  async registerShipmentThread(
    request: RegisterShipmentThreadRequest,
    tenantId?: string,
  ): Promise<CommercialQuoteDto> {
    const effectiveTenantId = this.requireTenant(tenantId || request.tenantId);
    const shipmentId = request.shipmentId?.trim();
    const sourceThreadId = request.sourceThreadId?.trim();
    if (!shipmentId || !sourceThreadId) {
      throw new BadRequestException('shipmentId and sourceThreadId are required');
    }

    const existing = await this.prisma.commercialQuote.findFirst({
      where: { tenantId: effectiveTenantId, shipmentId },
    });

    if (existing) {
      const updated = await this.prisma.commercialQuote.update({
        where: { id: existing.id },
        data: {
          sourceThreadId,
          customerId: request.customerId?.trim() || existing.customerId,
          originPort: request.originPort?.trim() || existing.originPort,
          destinationPort: request.destinationPort?.trim() || existing.destinationPort,
          weightKg: request.weightKg && request.weightKg > 0 ? request.weightKg : existing.weightKg,
          volumeCbm: request.volumeCbm !== undefined ? request.volumeCbm : existing.volumeCbm,
          transportMode: request.transportMode?.trim().toUpperCase() || existing.transportMode,
        },
      });
      return this.mapQuoteToDto(updated);
    }

    const created = await this.prisma.commercialQuote.create({
      data: {
        tenantId: effectiveTenantId,
        shipmentId,
        sourceThreadId,
        customerId: request.customerId?.trim() || null,
        originPort: request.originPort?.trim() || 'UNASSIGNED',
        destinationPort: request.destinationPort?.trim() || 'UNASSIGNED',
        weightKg: request.weightKg && request.weightKg > 0 ? request.weightKg : 1000,
        volumeCbm: request.volumeCbm ?? 0,
        transportMode: (request.transportMode || 'ROAD').toUpperCase(),
        status: 'PENDING_ROUTE',
        calculationMethod: 'PENDING_ROUTE',
      },
    });
    return this.mapQuoteToDto(created);
  }

  async createQuote(
    request: CreateQuoteRequest,
    tenantId?: string,
  ): Promise<CommercialQuoteDto> {
    const effectiveTenantId = this.requireTenant(tenantId || request.tenantId);
    const shipmentId = request.shipmentId?.trim();
    const routeId = request.routeId?.trim();
    if (!shipmentId || !routeId) {
      throw new BadRequestException('shipmentId and routeId are required to price a shipment quote');
    }

    let baseFreightCost = 0;
    let portHandlingFees = 0;
    let fuelSurchargeFee = 0;
    let cargoInsuranceFee = 0;
    let totalCustomsFee = 0;
    let totalEstimatedCost = 0;
    let currency = 'USD';
    let calculationMethod = '';
    let description = '';

    try {
      const estimate = await this.estimateCost(request, effectiveTenantId);
      baseFreightCost = estimate.baseFreightCost;
      portHandlingFees = estimate.portHandlingFees;
      fuelSurchargeFee = estimate.fuelSurchargeFee;
      cargoInsuranceFee = estimate.cargoInsuranceFee;
      totalCustomsFee = estimate.totalCustomsFee;
      totalEstimatedCost = estimate.totalEstimatedCost;
      currency = estimate.currency;
      calculationMethod = estimate.calculationMethod;
      description = estimate.description;
    } catch (error) {
      if (!(error instanceof NotFoundException) || !(request.distanceKm && request.distanceKm > 0)) {
        throw error;
      }
      const corridorCost = this.calculator.calculateRouteCorridorCost({
        distanceKm: request.distanceKm,
        weightKg: request.weightKg,
        volumeCbm: request.volumeCbm || 0,
        transportMode: request.transportMode,
      });
      baseFreightCost = corridorCost.baseFreightCost;
      portHandlingFees = corridorCost.portHandlingFees;
      fuelSurchargeFee = corridorCost.fuelSurchargeFee;
      totalEstimatedCost = corridorCost.totalEstimatedCost;
      calculationMethod = corridorCost.calculationMethod;
      description = corridorCost.description;
    }

    const rates = this.calculator.calculateMinAcceptableRate(
      totalEstimatedCost,
      request.minMarginPercent ?? DEFAULT_MIN_MARGIN_PERCENT,
      request.targetMarginPercent ?? DEFAULT_TARGET_MARGIN_PERCENT,
    );

    const existing = await this.prisma.commercialQuote.findFirst({
      where: { tenantId: effectiveTenantId, shipmentId },
    });

    const quotePayload = {
      sourceThreadId: request.sourceThreadId?.trim() || existing?.sourceThreadId || null,
      customerId: request.customerId?.trim() || existing?.customerId || null,
      routeId,
      routeName: request.routeName?.trim() || existing?.routeName || null,
      distanceKm: request.distanceKm && request.distanceKm > 0 ? request.distanceKm : (existing?.distanceKm ?? 0),
      originCountry: request.originCountry || existing?.originCountry || '',
      originPort: request.originPort,
      destinationCountry: request.destinationCountry || existing?.destinationCountry || '',
      destinationPort: request.destinationPort,
      weightKg: request.weightKg,
      volumeCbm: request.volumeCbm || 0,
      transportMode: (request.transportMode || existing?.transportMode || 'ROAD').toUpperCase(),
      cargoType: (request.cargoType || 'GENERAL').toUpperCase(),
      cargoValue: request.cargoValue || 0,
      hsCodes: request.hsCodes || [],
      baseFreightCost,
      portHandlingFees,
      fuelSurchargeFee,
      cargoInsuranceFee,
      totalCustomsFee,
      costPrice: totalEstimatedCost,
      minAcceptableRate: rates.minAcceptableRate,
      targetRate: rates.targetRate,
      currency,
      calculationMethod,
      description,
      status: existing?.status === 'CONFIRMED' ? 'CONFIRMED' : 'PRICED',
    };

    const saved = existing
      ? await this.prisma.commercialQuote.update({
          where: { id: existing.id },
          data: quotePayload,
        })
      : await this.prisma.commercialQuote.create({
          data: {
            tenantId: effectiveTenantId,
            shipmentId,
            ...quotePayload,
          },
        });

    return this.mapQuoteToDto(saved);
  }

  async listQuotes(
    request: ListQuotesRequest,
    tenantId?: string,
  ): Promise<ListQuotesResponse> {
    const effectiveTenantId = this.requireTenant(tenantId || request.tenantId);
    const where: Record<string, any> = { tenantId: effectiveTenantId };
    if (request.quoteId?.trim()) {
      where.id = request.quoteId.trim();
    }
    if (request.sourceThreadId?.trim()) {
      where.sourceThreadId = request.sourceThreadId.trim();
    }
    if (request.shipmentId?.trim()) {
      where.shipmentId = request.shipmentId.trim();
    }

    const rows = await this.prisma.commercialQuote.findMany({
      where,
      orderBy: { createdAt: 'desc' },
      take: 50,
    });

    return { quotes: rows.map((row) => this.mapQuoteToDto(row)) };
  }

  async confirmQuote(
    request: ConfirmQuoteRequest,
    tenantId?: string,
  ): Promise<CommercialQuoteDto> {
    const effectiveTenantId = this.requireTenant(tenantId || request.tenantId);
    const quoteId = request.quoteId?.trim();
    const shipmentId = request.shipmentId?.trim();
    if ((!quoteId && !shipmentId) || !Number.isFinite(request.agreedPrice) || request.agreedPrice <= 0) {
      throw new BadRequestException('quoteId or shipmentId and a positive agreedPrice are required');
    }

    const existing = await this.prisma.commercialQuote.findFirst({
      where: {
        tenantId: effectiveTenantId,
        ...(quoteId ? { id: quoteId } : { shipmentId }),
      },
    });
    if (!existing) {
      throw new NotFoundException(`Commercial quote ${quoteId || shipmentId} not found`);
    }
    if (existing.status === 'PENDING_ROUTE' || !existing.routeId || existing.targetRate <= 0) {
      throw new BadRequestException('Cannot confirm a quote before selecting a route and calculating pricing');
    }
    if (
      existing.status === 'CONFIRMED' &&
      existing.agreedPrice !== null &&
      Math.abs(existing.agreedPrice - request.agreedPrice) > 0.01
    ) {
      throw new ConflictException(
        `Quote ${existing.id} is already confirmed at $${existing.agreedPrice} ${existing.currency}`,
      );
    }

    const updated = await this.prisma.commercialQuote.update({
      where: { id: existing.id },
      data: {
        agreedPrice: Number(request.agreedPrice.toFixed(2)),
        status: 'CONFIRMED',
      },
    });
    return this.mapQuoteToDto(updated);
  }

  async getShipmentQuote(
    shipmentId: string,
    tenantId?: string,
    query?: {
      originPort?: string;
      destinationPort?: string;
      weightKg?: number | string;
      volumeCbm?: number | string;
      transportMode?: string;
      cargoType?: string;
    },
  ): Promise<{
    shipmentId: string;
    costPrice: number;
    listPrice: number;
    bottomPrice: number;
    currency: string;
  }> {
    const effectiveTenantId = this.requireTenant(tenantId);
    const normalizedShipmentId = shipmentId?.trim();
    if (!normalizedShipmentId) {
      throw new BadRequestException('shipmentId is required');
    }

    const persistedQuote = await this.prisma.commercialQuote?.findFirst?.({
      where: { tenantId: effectiveTenantId, shipmentId: normalizedShipmentId },
      orderBy: { createdAt: 'desc' },
    });
    if (persistedQuote) {
      if (
        persistedQuote.status === 'PENDING_ROUTE' ||
        !persistedQuote.routeId ||
        persistedQuote.targetRate <= 0
      ) {
        throw new BadRequestException(
          'Shipment route must be selected before pricing and negotiation',
        );
      }
      return {
        shipmentId: normalizedShipmentId,
        costPrice: persistedQuote.costPrice,
        listPrice: persistedQuote.targetRate,
        bottomPrice: persistedQuote.minAcceptableRate,
        currency: persistedQuote.currency || 'USD',
      };
    }

    if (query?.originPort && query?.destinationPort) {
      const parsedWeight = Number(query.weightKg);
      const parsedVolume =
        query.volumeCbm !== undefined && query.volumeCbm !== ''
          ? Number(query.volumeCbm)
          : NaN;
      const res = await this.getMinAcceptableRate(
        {
          originPort: query.originPort,
          destinationPort: query.destinationPort,
          weightKg: Number.isFinite(parsedWeight) && parsedWeight > 0 ? parsedWeight : 1000,
          volumeCbm: Number.isFinite(parsedVolume) && parsedVolume >= 0 ? parsedVolume : 1,
          transportMode: query.transportMode || 'SEA',
          cargoType: query.cargoType || 'GENERAL',
          minMarginPercent: 10,
          targetMarginPercent: 25,
        },
        effectiveTenantId,
      );
      return {
        shipmentId: normalizedShipmentId,
        costPrice: res.costPrice,
        listPrice: res.targetRate,
        bottomPrice: res.minAcceptableRate,
        currency: res.currency,
      };
    }

    const freightRate = await this.prisma.baseFreightRate.findFirst({
      where: { tenantId: effectiveTenantId },
      orderBy: { createdAt: 'desc' },
    });
    if (!freightRate) {
      throw new NotFoundException(`No authoritative quote rate found for shipment ${normalizedShipmentId}`);
    }
    if (freightRate.currency && freightRate.currency.toUpperCase() !== 'USD') {
      throw new BadRequestException('Freight rate currency conversion is required');
    }

    try {
      const res = await this.getMinAcceptableRate(
        {
          originPort: freightRate.originCode || 'VNSGN',
          destinationPort: freightRate.destinationCode || 'SGSIN',
          weightKg: 1000,
          volumeCbm: 1,
          transportMode: freightRate.transportMode || 'SEA',
          cargoType: freightRate.cargoType || 'GENERAL',
          minMarginPercent: 10,
          targetMarginPercent: 25,
        },
        effectiveTenantId,
      );
      return {
        shipmentId: normalizedShipmentId,
        costPrice: res.costPrice,
        listPrice: res.targetRate,
        bottomPrice: res.minAcceptableRate,
        currency: res.currency,
      };
    } catch (error) {
      if (!(error instanceof NotFoundException)) {
        throw error;
      }
      const costPrice = this.calculator.calculateFreightFee(
        1000,
        1,
        freightRate.ratePerKg,
        freightRate.ratePerCbm,
        freightRate.flatFee,
      );
      const rates = this.calculator.calculateMinAcceptableRate(costPrice, 10, 25);
      return {
        shipmentId: normalizedShipmentId,
        costPrice,
        listPrice: rates.targetRate,
        bottomPrice: rates.minAcceptableRate,
        currency: 'USD',
      };
    }
  }
}
