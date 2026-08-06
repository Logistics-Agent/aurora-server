import { Injectable, Logger } from '@nestjs/common';
import { PrismaService } from '../prisma/prisma.service';
import {
  EstimateCostRequest,
  EstimateCostResponse,
  GetCustomsDutyRequest,
  GetCustomsDutyResponse,
} from './dto/financial.dto';

@Injectable()
export class FinancialService {
  private readonly logger = new Logger(FinancialService.name);

  constructor(private readonly prisma: PrismaService) {}

  async estimateCost(
    request: EstimateCostRequest,
    tenantId?: string,
  ): Promise<EstimateCostResponse> {
    this.logger.log(
      `Estimating cost for route ${request.originPort} -> ${request.destinationPort} (Tenant: ${tenantId || 'global'})`,
    );

    const cargoType = request.cargoType || 'GENERAL';

    // ── 1. Base Freight Calculation ──────────────────────────────────────
    let baseFreightCost = 0;
    let calculationMethod = 'CORE_STANDARD_FORMULA';

    // Try finding specific route rate in DB
    const freightRate = await this.prisma.baseFreightRate.findFirst({
      where: {
        originPort: request.originPort,
        destinationPort: request.destinationPort,
        cargoType: cargoType,
        ...(tenantId ? { tenantId } : {}),
      },
    });

    if (freightRate) {
      const weightCost = request.weightKg * freightRate.ratePerKg;
      const volumeCost = request.volumeCbm * freightRate.ratePerCbm;
      baseFreightCost = Math.max(weightCost, volumeCost) + freightRate.flatFee;
      calculationMethod = `ROUTE_SPECIFIC_RATE_ID_${freightRate.id}`;
    } else {
      // Fallback standard calculation if route is not explicitly configured
      const fallbackRatePerKg = 0.8;
      const fallbackRatePerCbm = 35.0;
      const fallbackFlatFee = 200.0;

      const weightCost = request.weightKg * fallbackRatePerKg;
      const volumeCost = request.volumeCbm * fallbackRatePerCbm;
      baseFreightCost = Math.max(weightCost, volumeCost) + fallbackFlatFee;
      calculationMethod = 'FALLBACK_GLOBAL_FORMULA';
    }

    // ── 2. Port Handling Fees Calculation ──────────────────────────────
    const portHandlingFees = 120.0 + request.volumeCbm * 10.0;

    // ── 3. Customs Duties Calculation ──────────────────────────────────
    const estimatedCargoValue = request.weightKg * 2.5; // baseline valuation $2.5/kg
    let totalCustomsDuties = 0.0;
    const descriptions: string[] = [];

    if (request.hsCodes && request.hsCodes.length > 0) {
      for (const hsCode of request.hsCodes) {
        const dutyRate = await this.prisma.customsDutyRate.findUnique({
          where: {
            originCountry_destinationCountry_hsCode: {
              originCountry: request.originCountry,
              destinationCountry: request.destinationCountry,
              hsCode: hsCode,
            },
          },
        });

        if (dutyRate) {
          const duty = (dutyRate.dutyRatePercentage / 100.0) * estimatedCargoValue;
          totalCustomsDuties += duty;
          descriptions.push(
            `Duty ${dutyRate.dutyRatePercentage}% applied on HS Code ${hsCode} ($${duty.toFixed(2)})`,
          );
        } else {
          const fallbackDuty = 0.02 * estimatedCargoValue;
          totalCustomsDuties += fallbackDuty;
          descriptions.push(
            `Fallback 2% basic duty applied on HS Code ${hsCode} ($${fallbackDuty.toFixed(2)})`,
          );
        }
      }
    } else {
      totalCustomsDuties = 0.01 * estimatedCargoValue;
      descriptions.push(
        `Default 1% administrative customs fee applied ($${totalCustomsDuties.toFixed(2)})`,
      );
    }

    // ── 4. Total Cost Calculation ──────────────────────────────────────
    const totalEstimatedCost = baseFreightCost + portHandlingFees + totalCustomsDuties;
    const fullDescription =
      `Calculation: ${calculationMethod}. Port handling fees include base + volume rate. ` +
      `Customs duty details: ${descriptions.join('; ')}.`;

    return {
      baseFreightCost: Number(baseFreightCost.toFixed(2)),
      portHandlingFees: Number(portHandlingFees.toFixed(2)),
      customsDuties: Number(totalCustomsDuties.toFixed(2)),
      totalEstimatedCost: Number(totalEstimatedCost.toFixed(2)),
      currency: 'USD',
      calculationMethod,
      description: fullDescription,
    };
  }

  async getCustomsDuty(request: GetCustomsDutyRequest): Promise<GetCustomsDutyResponse> {
    this.logger.log(
      `Retrieving customs duty for ${request.hsCode} (${request.originCountry} -> ${request.destinationCountry})`,
    );

    const rate = await this.prisma.customsDutyRate.findUnique({
      where: {
        originCountry_destinationCountry_hsCode: {
          originCountry: request.originCountry,
          destinationCountry: request.destinationCountry,
          hsCode: request.hsCode,
        },
      },
    });

    if (rate) {
      return {
        hsCode: rate.hsCode,
        dutyRatePercentage: rate.dutyRatePercentage,
        description: rate.description || 'Standard import/export customs duty rate',
      };
    }

    return {
      hsCode: request.hsCode,
      dutyRatePercentage: 2.0,
      description: 'Default standard duty rate applied (HS Code specific rule not found)',
    };
  }
}
