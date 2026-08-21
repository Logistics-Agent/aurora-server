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
var OverdueInvoiceCronJob_1;
Object.defineProperty(exports, "__esModule", { value: true });
exports.OverdueInvoiceCronJob = void 0;
const common_1 = require("@nestjs/common");
const schedule_1 = require("@nestjs/schedule");
const prisma_service_1 = require("../prisma/prisma.service");
let OverdueInvoiceCronJob = OverdueInvoiceCronJob_1 = class OverdueInvoiceCronJob {
    constructor(prisma) {
        this.prisma = prisma;
        this.logger = new common_1.Logger(OverdueInvoiceCronJob_1.name);
    }
    async markOverdueInvoices() {
        const now = new Date();
        this.logger.log(`[CRON] Running OverdueInvoice scan at ${now.toISOString()}...`);
        try {
            const result = await this.prisma.invoice.updateMany({
                where: {
                    status: { in: ['UNPAID', 'PARTIALLY_PAID'] },
                    dueDate: { lt: now },
                },
                data: {
                    status: 'OVERDUE',
                },
            });
            this.logger.log(`[CRON] Marked ${result.count} invoice(s) as OVERDUE.`);
        }
        catch (error) {
            this.logger.error(`[CRON] Failed to mark overdue invoices: ${error.message}`);
        }
    }
};
exports.OverdueInvoiceCronJob = OverdueInvoiceCronJob;
__decorate([
    (0, schedule_1.Cron)('5 0 * * *'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", []),
    __metadata("design:returntype", Promise)
], OverdueInvoiceCronJob.prototype, "markOverdueInvoices", null);
exports.OverdueInvoiceCronJob = OverdueInvoiceCronJob = OverdueInvoiceCronJob_1 = __decorate([
    (0, common_1.Injectable)(),
    __metadata("design:paramtypes", [prisma_service_1.PrismaService])
], OverdueInvoiceCronJob);
//# sourceMappingURL=overdue-invoice.cron.js.map