import { Injectable, Logger, OnModuleDestroy, ServiceUnavailableException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as amqp from 'amqplib';
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
  customerId: string;
  invoiceId: string;
  paymentRecordId: string;
  amountPaid: number;
  paymentMethod: string;
  transactionRef: string;
  newInvoiceStatus: string;
  createdAt: string;
}

@Injectable()
export class RabbitMQMessagingService implements OnModuleDestroy {
  private readonly logger = new Logger(RabbitMQMessagingService.name);
  private connection: amqp.Connection | null = null;
  private channel: amqp.ConfirmChannel | null = null;
  private connecting: Promise<amqp.ConfirmChannel> | null = null;

  constructor(private readonly configService: ConfigService) {}

  async onModuleDestroy(): Promise<void> {
    try { await this.channel?.close(); } catch { /* already closed */ }
    try { await this.connection?.close(); } catch { /* already closed */ }
  }

  private async getChannel(): Promise<amqp.ConfirmChannel> {
    if (this.channel) return this.channel;
    if (this.connecting) return this.connecting;
    const uri = this.configService.get<string>('rabbitmq.uri');
    if (!uri) throw new ServiceUnavailableException('RabbitMQ is not configured');
    this.connecting = (async () => {
      const connection = await amqp.connect(uri);
      this.connection = connection;
      connection.on('close', () => { this.channel = null; this.connection = null; });
      try {
        const channel = await connection.createConfirmChannel();
        channel.on('close', () => { if (this.channel === channel) this.channel = null; });
        await channel.assertExchange('logistics_events', 'topic', { durable: true });
        this.channel = channel;
        return channel;
      } catch (error) {
        this.connection = null;
        await connection.close().catch(() => undefined);
        throw error;
      }
    })();
    try {
      return await this.connecting;
    } finally {
      this.connecting = null;
    }
  }

  private async publish<T>(routingKey: string, event: CloudEvent<T>): Promise<void> {
    const channel = await this.getChannel();
    await new Promise<void>((resolve, reject) => {
      channel.publish('logistics_events', routingKey, Buffer.from(JSON.stringify(event)), {
        persistent: true,
        contentType: 'application/json',
        messageId: event.id,
      }, (error) => error ? reject(error) : resolve());
    });
    this.logger.log(`Published ${routingKey} event ${event.id}`);
  }

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

    await this.publish('billing.invoice_created', cloudEvent);

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

    await this.publish('billing.payment_received', cloudEvent);

    return cloudEvent;
  }
}

