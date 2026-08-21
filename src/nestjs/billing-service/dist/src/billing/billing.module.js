"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.BillingModule = void 0;
const common_1 = require("@nestjs/common");
const billing_controller_1 = require("../interface/controllers/billing.controller");
const billing_service_1 = require("../application/services/billing.service");
const invoice_domain_service_1 = require("../domain/services/invoice.domain-service");
const generate_invoice_use_case_1 = require("../application/use-cases/generate-invoice.use-case");
const storage_service_1 = require("../infrastructure/storage/storage.service");
const rabbitmq_service_1 = require("../infrastructure/messaging/rabbitmq.service");
const shipment_completed_handler_1 = require("../infrastructure/messaging/event-handlers/shipment-completed.handler");
const financial_grpc_client_1 = require("../infrastructure/grpc-clients/financial.grpc-client");
const overdue_invoice_cron_1 = require("../infrastructure/jobs/overdue-invoice.cron");
const einvoice_adapter_1 = require("../infrastructure/einvoice/einvoice.adapter");
const dlq_service_1 = require("../infrastructure/messaging/dlq.service");
let BillingModule = class BillingModule {
};
exports.BillingModule = BillingModule;
exports.BillingModule = BillingModule = __decorate([
    (0, common_1.Module)({
        controllers: [billing_controller_1.BillingController],
        providers: [
            billing_service_1.BillingService,
            invoice_domain_service_1.InvoiceDomainService,
            generate_invoice_use_case_1.GenerateInvoiceUseCase,
            storage_service_1.StorageService,
            rabbitmq_service_1.RabbitMQMessagingService,
            shipment_completed_handler_1.ShipmentCompletedEventHandler,
            financial_grpc_client_1.FinancialGrpcClient,
            overdue_invoice_cron_1.OverdueInvoiceCronJob,
            einvoice_adapter_1.VNPTEInvoiceAdapter,
            dlq_service_1.DLQService,
        ],
        exports: [billing_service_1.BillingService, generate_invoice_use_case_1.GenerateInvoiceUseCase, einvoice_adapter_1.VNPTEInvoiceAdapter, dlq_service_1.DLQService],
    })
], BillingModule);
//# sourceMappingURL=billing.module.js.map