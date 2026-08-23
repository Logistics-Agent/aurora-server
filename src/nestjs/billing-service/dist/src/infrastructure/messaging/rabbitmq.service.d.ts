import { CloudEvent } from '../../common/events/cloud-event.factory';
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
export declare class RabbitMQMessagingService {
    private readonly logger;
    publishInvoiceCreated(event: InvoiceCreatedEvent, correlationId?: string): Promise<CloudEvent<InvoiceCreatedEvent>>;
    publishPaymentReceived(event: PaymentReceivedEvent, correlationId?: string): Promise<CloudEvent<PaymentReceivedEvent>>;
}
