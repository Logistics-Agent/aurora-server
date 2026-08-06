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
var BillingService_1;
Object.defineProperty(exports, "__esModule", { value: true });
exports.BillingService = void 0;
const common_1 = require("@nestjs/common");
const prisma_service_1 = require("../prisma/prisma.service");
let BillingService = BillingService_1 = class BillingService {
    constructor(prisma) {
        this.prisma = prisma;
        this.logger = new common_1.Logger(BillingService_1.name);
    }
    async createInvoice(request) {
        this.logger.log(`Creating invoice for shipment ${request.shipmentId} (Tenant: ${request.tenantId})`);
        const subtotal = request.items.reduce((sum, item) => sum + item.amount, 0);
        const taxAmount = subtotal * 0.05;
        const totalAmount = subtotal + taxAmount;
        const invoiceNumber = `INV-${new Date().getFullYear()}-${Math.floor(1000 + Math.random() * 9000)}`;
        const dueDate = request.dueDate ? new Date(request.dueDate) : new Date(Date.now() + 14 * 24 * 60 * 60 * 1000);
        const invoice = await this.prisma.invoice.create({
            data: {
                tenantId: request.tenantId,
                shipmentId: request.shipmentId,
                invoiceNumber: invoiceNumber,
                subtotal: Number(subtotal.toFixed(2)),
                taxAmount: Number(taxAmount.toFixed(2)),
                totalAmount: Number(totalAmount.toFixed(2)),
                status: 'UNPAID',
                dueDate: dueDate,
                items: {
                    create: request.items.map((item) => ({
                        description: item.description,
                        amount: item.amount,
                        category: item.category || 'FREIGHT',
                    })),
                },
            },
            include: {
                items: true,
            },
        });
        return this.mapInvoiceResponse(invoice);
    }
    async getInvoice(request) {
        const invoice = await this.prisma.invoice.findUnique({
            where: { id: request.invoiceId },
            include: { items: true },
        });
        if (!invoice) {
            throw new common_1.NotFoundException(`Invoice with ID ${request.invoiceId} not found`);
        }
        return this.mapInvoiceResponse(invoice);
    }
    async listInvoices(request) {
        const page = request.page && request.page > 0 ? request.page : 1;
        const limit = request.limit && request.limit > 0 ? request.limit : 10;
        const skip = (page - 1) * limit;
        const where = { tenantId: request.tenantId };
        if (request.status) {
            where.status = request.status;
        }
        const [invoices, totalItems] = await Promise.all([
            this.prisma.invoice.findMany({
                where,
                skip,
                take: limit,
                orderBy: { createdAt: 'desc' },
                include: { items: true },
            }),
            this.prisma.invoice.count({ where }),
        ]);
        return {
            invoices: invoices.map((inv) => this.mapInvoiceResponse(inv)),
            totalItems,
            page,
            limit,
        };
    }
    async updateInvoiceStatus(request) {
        const invoice = await this.prisma.invoice.update({
            where: { id: request.invoiceId },
            data: { status: request.status },
            include: { items: true },
        });
        return this.mapInvoiceResponse(invoice);
    }
    async createEscrowWallet(request) {
        this.logger.log(`Creating Escrow Wallet for Carrier ${request.carrierId} (Tenant: ${request.tenantId})`);
        const wallet = await this.prisma.escrowWallet.upsert({
            where: {
                tenantId_carrierId: {
                    tenantId: request.tenantId,
                    carrierId: request.carrierId,
                },
            },
            update: {},
            create: {
                tenantId: request.tenantId,
                carrierId: request.carrierId,
                balance: 5000.0,
                frozenAmount: 0.0,
                currency: request.currency || 'USD',
            },
        });
        return this.mapWalletResponse(wallet);
    }
    async getWalletBalance(request) {
        const wallet = await this.prisma.escrowWallet.findUnique({
            where: { id: request.walletId },
        });
        if (!wallet) {
            throw new common_1.NotFoundException(`Wallet with ID ${request.walletId} not found`);
        }
        return this.mapWalletResponse(wallet);
    }
    async freezeEscrowAmount(request) {
        this.logger.log(`Freezing $${request.amount} on wallet ${request.walletId} for shipment ${request.shipmentId}`);
        const wallet = await this.prisma.escrowWallet.findUnique({
            where: { id: request.walletId },
        });
        if (!wallet) {
            throw new common_1.NotFoundException(`Wallet with ID ${request.walletId} not found`);
        }
        const available = wallet.balance - wallet.frozenAmount;
        if (available < request.amount) {
            throw new common_1.BadRequestException(`Insufficient funds to freeze $${request.amount}. Available: $${available}`);
        }
        const [updatedWallet, transaction] = await this.prisma.$transaction([
            this.prisma.escrowWallet.update({
                where: { id: request.walletId },
                data: {
                    frozenAmount: { increment: request.amount },
                },
            }),
            this.prisma.escrowTransaction.create({
                data: {
                    walletId: request.walletId,
                    shipmentId: request.shipmentId,
                    type: 'FREEZE',
                    amount: request.amount,
                    status: 'SUCCESS',
                    referenceNo: request.referenceNo || `FREEZE-${request.shipmentId}`,
                },
            }),
        ]);
        return this.mapTransactionResponse(transaction);
    }
    async releaseEscrowAmount(request) {
        this.logger.log(`Releasing $${request.amount} on wallet ${request.walletId} for shipment ${request.shipmentId}`);
        const wallet = await this.prisma.escrowWallet.findUnique({
            where: { id: request.walletId },
        });
        if (!wallet) {
            throw new common_1.NotFoundException(`Wallet with ID ${request.walletId} not found`);
        }
        if (wallet.frozenAmount < request.amount) {
            throw new common_1.BadRequestException(`Cannot release $${request.amount}. Currently frozen: $${wallet.frozenAmount}`);
        }
        const [updatedWallet, transaction] = await this.prisma.$transaction([
            this.prisma.escrowWallet.update({
                where: { id: request.walletId },
                data: {
                    balance: { decrement: request.amount },
                    frozenAmount: { decrement: request.amount },
                },
            }),
            this.prisma.escrowTransaction.create({
                data: {
                    walletId: request.walletId,
                    shipmentId: request.shipmentId,
                    type: 'RELEASE',
                    amount: request.amount,
                    status: 'SUCCESS',
                    referenceNo: request.referenceNo || `RELEASE-${request.shipmentId}`,
                },
            }),
        ]);
        return this.mapTransactionResponse(transaction);
    }
    async refundEscrowAmount(request) {
        this.logger.log(`Refunding $${request.amount} on wallet ${request.walletId} for shipment ${request.shipmentId}`);
        const wallet = await this.prisma.escrowWallet.findUnique({
            where: { id: request.walletId },
        });
        if (!wallet) {
            throw new common_1.NotFoundException(`Wallet with ID ${request.walletId} not found`);
        }
        if (wallet.frozenAmount < request.amount) {
            throw new common_1.BadRequestException(`Cannot refund $${request.amount}. Currently frozen: $${wallet.frozenAmount}`);
        }
        const [updatedWallet, transaction] = await this.prisma.$transaction([
            this.prisma.escrowWallet.update({
                where: { id: request.walletId },
                data: {
                    frozenAmount: { decrement: request.amount },
                },
            }),
            this.prisma.escrowTransaction.create({
                data: {
                    walletId: request.walletId,
                    shipmentId: request.shipmentId,
                    type: 'REFUND',
                    amount: request.amount,
                    status: 'SUCCESS',
                    referenceNo: request.referenceNo || `REFUND-${request.shipmentId}`,
                },
            }),
        ]);
        return this.mapTransactionResponse(transaction);
    }
    mapInvoiceResponse(invoice) {
        return {
            id: invoice.id,
            tenantId: invoice.tenantId,
            shipmentId: invoice.shipmentId,
            invoiceNumber: invoice.invoiceNumber,
            subtotal: invoice.subtotal,
            taxAmount: invoice.taxAmount,
            totalAmount: invoice.totalAmount,
            status: invoice.status,
            dueDate: invoice.dueDate ? invoice.dueDate.toISOString() : '',
            createdAt: invoice.createdAt ? invoice.createdAt.toISOString() : '',
            items: (invoice.items || []).map((item) => ({
                id: item.id,
                description: item.description,
                amount: item.amount,
                category: item.category,
            })),
        };
    }
    mapWalletResponse(wallet) {
        return {
            walletId: wallet.id,
            tenantId: wallet.tenantId,
            carrierId: wallet.carrierId,
            balance: wallet.balance,
            frozenAmount: wallet.frozenAmount,
            availableAmount: Number((wallet.balance - wallet.frozenAmount).toFixed(2)),
            currency: wallet.currency,
        };
    }
    mapTransactionResponse(tx) {
        return {
            transactionId: tx.id,
            walletId: tx.walletId,
            shipmentId: tx.shipmentId,
            type: tx.type,
            amount: tx.amount,
            status: tx.status,
            referenceNo: tx.referenceNo || '',
            createdAt: tx.createdAt ? tx.createdAt.toISOString() : '',
        };
    }
};
exports.BillingService = BillingService;
exports.BillingService = BillingService = BillingService_1 = __decorate([
    (0, common_1.Injectable)(),
    __metadata("design:paramtypes", [prisma_service_1.PrismaService])
], BillingService);
//# sourceMappingURL=billing.service.js.map