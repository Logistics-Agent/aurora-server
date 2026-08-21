import { Injectable, Logger } from '@nestjs/common';
import { CloudEventFactory, CloudEvent } from '../../common/events/cloud-event.factory';

export interface InvoiceCreatedEvent {
  tenantId: string;
  invoiceId: string;
  invoiceNumber: string;
  shipmentId: string;
  customerId: string;
  totalAmount: number;
  currency: string;
  dueDate: string;
  pdfUrl: string;
  createdAt: string;
}

export interface PaymentReceivedEvent {
  tenantId: string;
  invoiceId: string;
  paymentRecordId: string;
  amountPaid: number;
  paymentMethod: string;
  transactionRef: string;
  newInvoiceStatus: string;
  createdAt: string;
}

@Injectable()
export class RabbitMQMessagingService {
  private readonly logger = new Logger(RabbitMQMessagingService.name);

  async publishInvoiceCreated(
    event: InvoiceCreatedEvent,
    correlationId?: string,
  ): Promise<CloudEvent<InvoiceCreatedEvent>> {
    const cloudEvent = CloudEventFactory.create(
      'com.aurora.billing.invoice.issued',
      '/services/billing-service',
      event.tenantId,
      correlationId,
      event,
    );

    this.logger.log(
      `[CloudEvent Published] Topic 'billing.invoice_created' | ID: ${cloudEvent.id} | Invoice ${event.invoiceNumber} ($${event.totalAmount}) for Tenant ${event.tenantId}`,
    );

    return cloudEvent;
  }

  async publishPaymentReceived(
    event: PaymentReceivedEvent,
    correlationId?: string,
  ): Promise<CloudEvent<PaymentReceivedEvent>> {
    const cloudEvent = CloudEventFactory.create(
      'com.aurora.billing.payment.received',
      '/services/billing-service',
      event.tenantId,
      correlationId,
      event,
    );

    this.logger.log(
      `[CloudEvent Published] Topic 'billing.payment_received' | ID: ${cloudEvent.id} | Payment $${event.amountPaid} for Invoice ${event.invoiceId} (Status: ${event.newInvoiceStatus})`,
    );

    return cloudEvent;
  }
}

