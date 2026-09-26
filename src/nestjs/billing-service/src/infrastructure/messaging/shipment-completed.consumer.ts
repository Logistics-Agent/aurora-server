import { Injectable, Logger, OnModuleDestroy, OnModuleInit } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as amqp from 'amqplib';
import { ShipmentCompletedEvent, ShipmentCompletedEventHandler } from './event-handlers/shipment-completed.handler';

@Injectable()
export class ShipmentCompletedConsumer implements OnModuleInit, OnModuleDestroy {
  private readonly logger = new Logger(ShipmentCompletedConsumer.name);
  private connection: amqp.Connection | null = null;
  private channel: amqp.Channel | null = null;
  private retryTimer: NodeJS.Timeout | null = null;
  private stopping = false;

  constructor(
    private readonly config: ConfigService,
    private readonly handler: ShipmentCompletedEventHandler,
  ) {}

  async onModuleInit(): Promise<void> {
    await this.connect();
  }

  async onModuleDestroy(): Promise<void> {
    this.stopping = true;
    if (this.retryTimer) clearTimeout(this.retryTimer);
    try { await this.channel?.close(); } catch { /* connection may already be closed */ }
    try { await this.connection?.close(); } catch { /* connection may already be closed */ }
  }

  private scheduleRetry(): void {
    if (this.stopping || this.retryTimer) return;
    this.retryTimer = setTimeout(() => {
      this.retryTimer = null;
      void this.connect();
    }, 5000);
  }

  private async connect(): Promise<void> {
    const uri = this.config.get<string>('rabbitmq.uri');
    if (!uri) {
      this.logger.error('RabbitMQ is not configured; shipment events cannot be consumed');
      return;
    }
    try {
      const connection = await amqp.connect(uri);
      connection.on('close', () => {
        this.connection = null;
        this.channel = null;
        this.scheduleRetry();
      });
      connection.on('error', (error: Error) => this.logger.warn(`RabbitMQ error: ${error.message}`));
      this.connection = connection;
      const channel = await connection.createChannel();
      this.channel = channel;

      await channel.assertExchange('logistics_events', 'topic', { durable: true });
      await channel.assertExchange('billing_shipment_events_dlx', 'fanout', { durable: true });
      await channel.assertQueue('billing_shipment_events_dlq', { durable: true });
      await channel.bindQueue('billing_shipment_events_dlq', 'billing_shipment_events_dlx', '');
      await channel.assertQueue('billing_shipment_events', {
        durable: true,
        arguments: { 'x-dead-letter-exchange': 'billing_shipment_events_dlx' },
      });
      for (const key of ['shipment.completed', 'shipment.pod_uploaded']) {
        await channel.bindQueue('billing_shipment_events', 'logistics_events', key);
      }
      await channel.prefetch(10);
      await channel.consume('billing_shipment_events', async (message) => {
        if (!message) return;
        try {
          const envelope = JSON.parse(message.content.toString());
          const data = envelope.data || envelope;
          const tenantId = envelope.tenant_id || data.tenantId;
          if (!tenantId || (data.tenantId && data.tenantId !== tenantId)) {
            throw new Error('Shipment event has missing or inconsistent tenantId');
          }
          await this.handler.handle({ ...data, tenantId } as ShipmentCompletedEvent);
          channel.ack(message);
        } catch (error) {
          this.logger.error(`Shipment event rejected: ${(error as Error).message}`);
          channel.nack(message, false, false);
        }
      });
      this.logger.log('Consuming shipment.completed and shipment.pod_uploaded events');
    } catch (error) {
      this.logger.warn(`RabbitMQ unavailable: ${(error as Error).message}`);
      try { await this.channel?.close(); } catch { /* connection may already be closed */ }
      try { await this.connection?.close(); } catch { /* connection may already be closed */ }
      this.channel = null;
      this.connection = null;
      this.scheduleRetry();
    }
  }
}
