"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var __metadata = (this && this.__metadata) || function (k, v) {
    if (typeof Reflect === "object" && typeof Reflect.metadata === "function") return Reflect.metadata(k, v);
};
var FinancialService_1;
Object.defineProperty(exports, "__esModule", { value: true });
exports.FinancialService = void 0;
const common_1 = require("@nestjs/common");
const prisma_service_1 = require("../prisma/prisma.service");
let FinancialService = FinancialService_1 = class FinancialService {
    constructor(prisma) {
        this.prisma = prisma;
        this.logger = new common_1.Logger(FinancialService_1.name);
    }
    async estimateCost(request, tenantId) {
        this.logger.log(`Estimating cost for route ${request.originPort} -> ${request.destinationPort} (Tenant: ${tenantId || 'global'})`);
        const cargoType = request.cargoType || 'GENERAL';
        let baseFreightCost = 0;
        let calculationMethod = 'CORE_STANDARD_FORMULA';
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
        }
        else {
            const fallbackRatePerKg = 0.8;
            const fallbackRatePerCbm = 35.0;
            const fallbackFlatFee = 200.0;
            const weightCost = request.weightKg * fallbackRatePerKg;
            const volumeCost = request.volumeCbm * fallbackRatePerCbm;
            baseFreightCost = Math.max(weightCost, volumeCost) + fallbackFlatFee;
            calculationMethod = 'FALLBACK_GLOBAL_FORMULA';
        }
        const portHandlingFees = 120.0 + request.volumeCbm * 10.0;
        const estimatedCargoValue = request.weightKg * 2.5;
        let totalCustomsDuties = 0.0;
        const descriptions = [];
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
                    descriptions.push(`Duty ${dutyRate.dutyRatePercentage}% applied on HS Code ${hsCode} ($${duty.toFixed(2)})`);
                }
                else {
                    const fallbackDuty = 0.02 * estimatedCargoValue;
                    totalCustomsDuties += fallbackDuty;
                    descriptions.push(`Fallback 2% basic duty applied on HS Code ${hsCode} ($${fallbackDuty.toFixed(2)})`);
                }
            }
        }
        else {
            totalCustomsDuties = 0.01 * estimatedCargoValue;
            descriptions.push(`Default 1% administrative customs fee applied ($${totalCustomsDuties.toFixed(2)})`);
        }
        const totalEstimatedCost = baseFreightCost + portHandlingFees + totalCustomsDuties;
        const fullDescription = `Calculation: ${calculationMethod}. Port handling fees include base + volume rate. ` +
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
    async getCustomsDuty(request) {
        this.logger.log(`Retrieving customs duty for ${request.hsCode} (${request.originCountry} -> ${request.destinationCountry})`);
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
};
exports.FinancialService = FinancialService;
exports.FinancialService = FinancialService = FinancialService_1 = __decorate([
    (0, common_1.Injectable)(),
    __metadata("design:paramtypes", [prisma_service_1.PrismaService])
], FinancialService);
//# sourceMappingURL=financial.service.js.map