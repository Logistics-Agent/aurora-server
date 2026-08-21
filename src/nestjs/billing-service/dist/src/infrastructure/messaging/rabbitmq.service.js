"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var RabbitMQMessagingService_1;
Object.defineProperty(exports, "__esModule", { value: true });
exports.RabbitMQMessagingService = void 0;
const common_1 = require("@nestjs/common");
const cloud_event_factory_1 = require("../../common/events/cloud-event.factory");
let RabbitMQMessagingService = RabbitMQMessagingService_1 = class RabbitMQMessagingService {
    constructor() {
        this.logger = new common_1.Logger(RabbitMQMessagingService_1.name);
    }
    async publishInvoiceCreated(event, correlationId) {
        const cloudEvent = cloud_event_factory_1.CloudEventFactory.create('com.aurora.billing.invoice.issued', '/services/billing-service', event.tenantId, correlationId, event);
        this.logger.log(`[CloudEvent Published] Topic 'billing.invoice_created' | ID: ${cloudEvent.id} | Invoice ${event.invoiceNumber} ($${event.totalAmount}) for Tenant ${event.tenantId}`);
        return cloudEvent;
    }
    async publishPaymentReceived(event, correlationId) {
        const cloudEvent = cloud_event_factory_1.CloudEventFactory.create('com.aurora.billing.payment.received', '/services/billing-service', event.tenantId, correlationId, event);
        this.logger.log(`[CloudEvent Published] Topic 'billing.payment_received' | ID: ${cloudEvent.id} | Payment $${event.amountPaid} for Invoice ${event.invoiceId} (Status: ${event.newInvoiceStatus})`);
        return cloudEvent;
    }
};
exports.RabbitMQMessagingService = RabbitMQMessagingService;
exports.RabbitMQMessagingService = RabbitMQMessagingService = RabbitMQMessagingService_1 = __decorate([
    (0, common_1.Injectable)()
], RabbitMQMessagingService);
//# sourceMappingURL=rabbitmq.service.js.map