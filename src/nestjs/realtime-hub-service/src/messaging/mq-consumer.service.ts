import { Injectable, OnModuleInit, OnModuleDestroy, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as amqp from 'amqplib';
import { EventsGateway } from '../gateway/events.gateway';

@Injectable()
export class MQConsumerService implements OnModuleInit, OnModuleDestroy {
  private readonly logger = new Logger(MQConsumerService.name);
  private connection: any;
  private channel: any;

  constructor(
    private readonly configService: ConfigService,
    private readonly eventsGateway: EventsGateway,
  ) {}

  async onModuleInit() {
    await this.connectAndSubscribe();
  }

  async onModuleDestroy() {
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

      this.channel.consume(queue, (msg: amqp.ConsumeMessage | null) => {
        if (msg) {
          this.handleIncomingMessage(msg.fields.routingKey, msg.content.toString());
          this.channel.ack(msg);
        }
      });

      this.logger.log('RabbitMQ Consumer initialized successfully.');
    } catch (error) {
      this.logger.warn(`Could not connect to RabbitMQ (${error.message}). Realtime Hub waiting in offline mode.`);
    }
  }

  /**
   * Routes RabbitMQ Events to targeted WebSocket Rooms
   */
  handleIncomingMessage(routingKey: string, content: string) {
    this.logger.log(`Received MQ Event [RoutingKey: ${routingKey}]`);

    try {
      const parsed = JSON.parse(content);
      const tenantId = parsed.tenantId || 'a0000000-0000-0000-0000-000000000001';
      const shipmentId = parsed.shipmentId;
      const customerId = parsed.customerId || parsed.userId;

      if (routingKey.startsWith('billing.')) {
        const eventName = routingKey.replace('billing.', '').toUpperCase();
        if (customerId) {
          this.eventsGateway.sendToUser(tenantId, customerId, eventName, parsed);
        }
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
    } catch (err) {
      this.logger.error(`Failed to parse RabbitMQ payload (${err.message}): ${content}`);
    }
  }
}
