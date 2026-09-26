import { Injectable, OnModuleInit, OnModuleDestroy, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as amqp from 'amqplib';
import { EventsGateway } from '../gateway/events.gateway';

@Injectable()
export class MQConsumerService implements OnModuleInit, OnModuleDestroy {
  private readonly logger = new Logger(MQConsumerService.name);
  private connection: any;
  private channel: any;
  private reconnectTimer: NodeJS.Timeout | null = null;
  private stopping = false;

  constructor(
    private readonly configService: ConfigService,
    private readonly eventsGateway: EventsGateway,
  ) {}

  async onModuleInit() {
    await this.connectAndSubscribe();
  }

  async onModuleDestroy() {
    this.stopping = true;
    if (this.reconnectTimer) clearTimeout(this.reconnectTimer);
    try {
      if (this.channel) await this.channel.close();
      if (this.connection) await this.connection.close();
    } catch (e) {
      // Ignore cleanup error on shutdown
    }
  }

  private async connectAndSubscribe() {
    const rabbitMqUri = this.configService.get<string>(
      'rabbitmq.uri',
      'amqp://guest:guest@localhost:5672',
    );

    try {
      this.logger.log(`Connecting to RabbitMQ at ${rabbitMqUri}...`);
      this.connection = await amqp.connect(rabbitMqUri);
      this.connection.on('close', () => {
        this.connection = null;
        this.channel = null;
        this.scheduleReconnect();
      });
      this.connection.on('error', (error: Error) => this.logger.warn(`RabbitMQ connection error: ${error.message}`));
      this.channel = await this.connection.createChannel();

      const exchange = 'logistics_events';
      const queue = 'realtime_hub_queue';

      await this.channel.assertExchange(exchange, 'topic', { durable: true });
      await this.channel.assertQueue(queue, { durable: true });

      // Bind routing keys — đảm bảo sync với tất cả microservice phát event
      const bindings = ['billing.#', 'negotiation.#', 'shipment.#', 'financial.#'];
      for (const pattern of bindings) {
        await this.channel.bindQueue(queue, exchange, pattern);
        this.logger.log(`Bound queue '${queue}' to exchange '${exchange}' with pattern '${pattern}'`);
      }

      await this.channel.prefetch(20);
      await this.channel.consume(queue, async (msg: amqp.ConsumeMessage | null) => {
        if (msg) {
          try {
            await this.handleIncomingMessage(msg.fields.routingKey, msg.content.toString());
            this.channel.ack(msg);
          } catch (error) {
            this.logger.error(`Realtime event handling failed: ${error.message}`);
            this.channel.nack(msg, false, false);
          }
        }
      });

      this.logger.log('RabbitMQ Consumer initialized successfully.');
    } catch (error) {
      this.logger.warn(`Could not connect to RabbitMQ (${error.message}). Realtime Hub waiting in offline mode.`);
      this.scheduleReconnect();
    }
  }

  private scheduleReconnect(): void {
    if (this.stopping || this.reconnectTimer) return;
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      void this.connectAndSubscribe();
    }, 5000);
  }

  /**
   * Routes RabbitMQ Events to targeted WebSocket Rooms
   */
  async handleIncomingMessage(routingKey: string, content: string): Promise<void> {
    this.logger.log(`Received MQ Event [RoutingKey: ${routingKey}]`);

    const envelope = JSON.parse(content);
    const parsed = envelope.data || envelope;
    const tenantId = envelope.tenant_id || parsed.tenantId;
    if (!tenantId || (parsed.tenantId && parsed.tenantId !== tenantId)) {
      throw new Error('Realtime event has missing or inconsistent tenantId');
    }
    const shipmentId = parsed.shipmentId;
    const customerId = parsed.customerId;
    const userId = parsed.userId;

      if (routingKey.startsWith('billing.')) {
        const eventName = routingKey.replace('billing.', '').toUpperCase();
        if (userId) await this.eventsGateway.sendToUser(tenantId, userId, eventName, parsed);
        if (customerId) this.eventsGateway.sendToCustomer(tenantId, customerId, eventName, parsed);
        this.eventsGateway.sendToTenant(tenantId, eventName, parsed);
      } else if (routingKey.startsWith('negotiation.')) {
        const eventName = routingKey.replace('negotiation.', '').toUpperCase();
        if (shipmentId) {
          this.eventsGateway.sendToShipment(tenantId, shipmentId, eventName, parsed);
        } else {
          this.eventsGateway.sendToTenant(tenantId, eventName, parsed);
        }
      } else if (routingKey.startsWith('shipment.')) {
        const eventName = routingKey.replace('shipment.', '').toUpperCase();
        if (shipmentId) {
          this.eventsGateway.sendToShipment(tenantId, shipmentId, eventName, parsed);
        } else {
          this.eventsGateway.sendToTenant(tenantId, eventName, parsed);
        }
      } else {
        this.eventsGateway.sendToTenant(tenantId, routingKey.toUpperCase(), parsed);
      }
  }
}
