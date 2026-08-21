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
var GenerateInvoiceUseCase_1;
Object.defineProperty(exports, "__esModule", { value: true });
exports.GenerateInvoiceUseCase = void 0;
const common_1 = require("@nestjs/common");
const prisma_service_1 = require("../../infrastructure/prisma/prisma.service");
const invoice_domain_service_1 = require("../../domain/services/invoice.domain-service");
const financial_grpc_client_1 = require("../../infrastructure/grpc-clients/financial.grpc-client");
const storage_service_1 = require("../../infrastructure/storage/storage.service");
const rabbitmq_service_1 = require("../../infrastructure/messaging/rabbitmq.service");
const config_1 = require("@nestjs/config");
let GenerateInvoiceUseCase = GenerateInvoiceUseCase_1 = class GenerateInvoiceUseCase {
    constructor(prisma, domainService, financialGrpcClient, storageService, messagingService, configService) {
        this.prisma = prisma;
        this.domainService = domainService;
        this.financialGrpcClient = financialGrpcClient;
        this.storageService = storageService;
        this.messagingService = messagingService;
        this.configService = configService;
        this.logger = new common_1.Logger(GenerateInvoiceUseCase_1.name);
    }
    async execute(input) {
        this.logger.log(`Executing GenerateInvoiceUseCase for shipment ${input.shipmentId} (Tenant: ${input.tenantId})`);
        const existing = await this.prisma.invoice.findFirst({
            where: {
                tenantId: input.tenantId,
                shipmentId: input.shipmentId,
            },
        });
        if (existing) {
            throw new common_1.ConflictException(`Invoice ${existing.invoiceNumber} already generated for shipment ${input.shipmentId}`);
        }
        const costEstimate = await this.financialGrpcClient.estimateCost({
            tenantId: input.tenantId,
            originCountry: 'CN',
            originPort: input.originPort || 'SGSIN',
            destinationCountry: 'VN',
            destinationPort: input.destinationPort || 'VNSGN',
            weightKg: input.weightKg || 1000,
            volumeCbm: input.volumeCbm || 5,
        });
        const itemsInput = [
            {
                description: `Base Freight Charge (${input.originPort || 'SGSIN'} -> ${input.destinationPort || 'VNSGN'})`,
                quantity: 1,
                unitPrice: costEstimate.baseFreightCost,
                amount: costEstimate.baseFreightCost,
                category: 'FREIGHT',
            },
            {
                description: 'Port & Terminal Handling Charge (THC / DOC)',
                quantity: 1,
                unitPrice: costEstimate.portHandlingFees,
                amount: costEstimate.portHandlingFees,
                category: 'PORT_FEE',
            },
            {
                description: `Customs Duty & Import Tax (${costEstimate.description})`,
                quantity: 1,
                unitPrice: costEstimate.totalCustomsFee,
                amount: costEstimate.totalCustomsFee,
                category: 'CUSTOMS_DUTY',
            },
        ];
        const totals = this.domainService.calculateInvoiceTotals(itemsInput, 5.0);
        const invoiceCount = await this.prisma.invoice.count({
            where: { tenantId: input.tenantId },
        });
        const invoiceNumber = this.domainService.generateInvoiceNumber(invoiceCount + 1);
        const paymentTermsDays = input.paymentTermsDays ||
            this.configService.get('billing.defaultPaymentTermsDays', 30);
        const dueDate = this.domainService.calculateDueDate(new Date(), paymentTermsDays);
        const createdInvoice = await this.prisma.$transaction(async (tx) => {
            return tx.invoice.create({
                data: {
                    tenantId: input.tenantId,
                    shipmentId: input.shipmentId,
                    customerId: input.customerId || 'CUST-001',
                    invoiceNumber: invoiceNumber,
                    subtotal: totals.subtotal,
                    taxAmount: totals.taxAmount,
                    totalAmount: totals.totalAmount,
                    currency: costEstimate.currency || 'USD',
                    status: 'UNPAID',
                    dueDate: dueDate,
                    podS3Key: input.podS3Key || null,
                    items: {
                        create: itemsInput.map((item) => ({
                            description: item.description,
                            quantity: item.quantity,
                            unitPrice: item.unitPrice,
                            amount: item.amount,
                            category: item.category,
                        })),
                    },
                },
                include: {
                    items: true,
                },
            });
        });
        const pdfResult = await this.storageService.renderAndUploadInvoicePdf(input.tenantId, createdInvoice.id, createdInvoice.invoiceNumber);
        const updatedInvoice = await this.prisma.invoice.update({
            where: { id: createdInvoice.id },
            data: {
                pdfS3Key: pdfResult.s3Key,
                pdfUrl: pdfResult.presignedUrl,
            },
            include: { items: true },
        });
        await this.messagingService.publishInvoiceCreated({
            tenantId: updatedInvoice.tenantId,
            invoiceId: updatedInvoice.id,
            invoiceNumber: updatedInvoice.invoiceNumber,
            shipmentId: updatedInvoice.shipmentId,
            customerId: updatedInvoice.customerId,
            totalAmount: updatedInvoice.totalAmount,
            currency: updatedInvoice.currency,
            dueDate: updatedInvoice.dueDate.toISOString(),
            pdfUrl: updatedInvoice.pdfUrl || '',
            createdAt: updatedInvoice.createdAt.toISOString(),
        });
        return updatedInvoice;
    }
};
exports.GenerateInvoiceUseCase = GenerateInvoiceUseCase;
exports.GenerateInvoiceUseCase = GenerateInvoiceUseCase = GenerateInvoiceUseCase_1 = __decorate([
    (0, common_1.Injectable)(),
    __metadata("design:paramtypes", [prisma_service_1.PrismaService,
        invoice_domain_service_1.InvoiceDomainService,
        financial_grpc_client_1.FinancialGrpcClient,
        storage_service_1.StorageService,
        rabbitmq_service_1.RabbitMQMessagingService,
        config_1.ConfigService])
], GenerateInvoiceUseCase);
//# sourceMappingURL=generate-invoice.use-case.js.map