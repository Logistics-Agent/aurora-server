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
var ShipmentCompletedEventHandler_1;
Object.defineProperty(exports, "__esModule", { value: true });
exports.ShipmentCompletedEventHandler = void 0;
const common_1 = require("@nestjs/common");
const prisma_service_1 = require("../../prisma/prisma.service");
const generate_invoice_use_case_1 = require("../../../application/use-cases/generate-invoice.use-case");
let ShipmentCompletedEventHandler = ShipmentCompletedEventHandler_1 = class ShipmentCompletedEventHandler {
    constructor(prisma, generateInvoiceUseCase) {
        this.prisma = prisma;
        this.generateInvoiceUseCase = generateInvoiceUseCase;
        this.logger = new common_1.Logger(ShipmentCompletedEventHandler_1.name);
    }
    async handle(event) {
        this.logger.log(`Processing POD event for Shipment ID ${event.shipmentId} (Tenant: ${event.tenantId})`);
        if (!event.podDocumentS3Key) {
            this.logger.warn(`[POD Validation] Shipment ${event.shipmentId} event missing 'podDocumentS3Key'. Official invoice generation skipped until POD is uploaded.`);
            return;
        }
        const existingInvoice = await this.prisma.invoice.findFirst({
            where: {
                tenantId: event.tenantId,
                shipmentId: event.shipmentId,
            },
        });
        if (existingInvoice) {
            this.logger.warn(`[Idempotent Check] Invoice ${existingInvoice.invoiceNumber} already exists for shipment ${event.shipmentId}. Skipping event replay.`);
            return;
        }
        await this.generateInvoiceUseCase.execute({
            tenantId: event.tenantId,
            shipmentId: event.shipmentId,
            customerId: event.customerId || 'CUST-001',
            originPort: event.originPort || 'SGSIN',
            destinationPort: event.destinationPort || 'VNSGN',
            weightKg: event.weightKg || 1000,
            volumeCbm: event.volumeCbm || 5,
            podS3Key: event.podDocumentS3Key,
        });
    }
};
exports.ShipmentCompletedEventHandler = ShipmentCompletedEventHandler;
exports.ShipmentCompletedEventHandler = ShipmentCompletedEventHandler = ShipmentCompletedEventHandler_1 = __decorate([
    (0, common_1.Injectable)(),
    __metadata("design:paramtypes", [prisma_service_1.PrismaService,
        generate_invoice_use_case_1.GenerateInvoiceUseCase])
], ShipmentCompletedEventHandler);
//# sourceMappingURL=shipment-completed.handler.js.map