import { Injectable, Logger, OnModuleInit, OnModuleDestroy, Inject } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as amqp from 'amqplib';
import {
  IConversationRepository,
  CONVERSATION_REPOSITORY,
} from '../../domain/repositories/conversation.repository.interface';
import { AiGovernanceGrpcClient } from '../../infrastructure/grpc/ai-governance.grpc-client';
import { ConversationalPromptBuilder } from '../prompt/conversational-prompt-builder';
import { CurrentUser } from '../../infrastructure/security/current-user.interface';
import { ActorType } from '../../domain/enums/actor-type.enum';

export interface ConversationSummaryRequestedEvent {
  conversationId: string;
  tenantId: string;
  userId: string;
  upToSequenceNumber: number;
  traceId?: string;
}

@Injectable()
export class ConversationSummaryService implements OnModuleInit, OnModuleDestroy {
  private readonly logger = new Logger(ConversationSummaryService.name);
  private connection: any;
  private channel: any;
  private readonly queueName = 'assistant_summary_queue';
  private readonly exchangeName = 'logistics_events';
  private readonly routingKey = 'assistant.summary.requested';
  private isConnected = false;
  private readonly isProduction: boolean;

  constructor(
    private readonly configService: ConfigService,
    @Inject(CONVERSATION_REPOSITORY)
    private readonly conversationRepo: IConversationRepository,
    private readonly aiGovernanceClient: AiGovernanceGrpcClient,
    private readonly promptBuilder: ConversationalPromptBuilder,
  ) {
    this.isProduction = (this.configService.get<string>('NODE_ENV') || 'development') === 'production';
  }

  async onModuleInit() {
    await this.initRabbitMq();
  }

  async onModuleDestroy() {
    try {
      if (this.channel) await this.channel.close();
      if (this.connection) await this.connection.close();
    } catch {
      // ignore
    }
  }

  private async initRabbitMq() {
    const rabbitMqUri =
      this.configService.get<string>('RABBITMQ_URI') ||
      this.configService.get<string>('rabbitmq.uri') ||
      'amqp://guest:guest@localhost:5672';

    try {
      this.connection = await amqp.connect(rabbitMqUri);
      this.channel = await this.connection.createChannel();

      await this.channel.assertExchange(this.exchangeName, 'topic', { durable: true });
      await this.channel.assertQueue(this.queueName, { durable: true });
      await this.channel.bindQueue(this.queueName, this.exchangeName, this.routingKey);

      this.channel.prefetch(2);
      this.channel.consume(this.queueName, async (msg: amqp.ConsumeMessage | null) => {
        if (msg) {
          try {
            const event: ConversationSummaryRequestedEvent = JSON.parse(msg.content.toString());
            await this.handleSummaryJob(event);
            this.channel.ack(msg);
          } catch (err) {
            this.logger.error(`[Observability] assistant_summary_failed for conv ${msg.content.toString()}: ${err}`);
            this.channel.nack(msg, false, false);
          }
        }
      });

      this.isConnected = true;
      this.logger.log(`[ConversationSummaryService] Connected to RabbitMQ on queue ${this.queueName}`);
    } catch (err) {
      this.isConnected = false;
      if (this.isProduction) {
        this.logger.warn(`[Observability] SUMMARY_QUEUE_UNAVAILABLE: RabbitMQ not reachable in production (${(err as Error).message}). Summaries will be deferred.`);
      } else {
        this.logger.warn(`[ConversationSummaryService] RabbitMQ not reachable in development (${(err as Error).message}). Using in-process worker mode.`);
      }
    }
  }

  async enqueueSummaryJob(event: ConversationSummaryRequestedEvent): Promise<void> {
    if (this.isConnected && this.channel) {
      try {
        const payload = Buffer.from(JSON.stringify(event));
        this.channel.publish(this.exchangeName, this.routingKey, payload, { persistent: true });
        this.logger.log(`[Observability] assistant_summary_enqueued: conv ${event.conversationId} up to seq ${event.upToSequenceNumber}`);
        return;
      } catch (err) {
        this.logger.warn(`[Observability] SUMMARY_QUEUE_UNAVAILABLE: RabbitMQ publish failed for conv ${event.conversationId}: ${err}`);
      }
    }

    // PATCH 2: In Production, DO NOT execute in-process summary! Defer safely.
    if (this.isProduction) {
      this.logger.warn(`[Observability] SUMMARY_QUEUE_UNAVAILABLE: Summary deferred for conversation ${event.conversationId}. Chat continues uninterrupted.`);
      return;
    }

    // In-process worker allowed ONLY in development/test
    setImmediate(() => {
      this.handleSummaryJob(event).catch((err) => {
        this.logger.error(`[Observability] assistant_summary_failed in-process: ${err}`);
      });
    });
  }

  async handleSummaryJob(event: ConversationSummaryRequestedEvent): Promise<void> {
    const { conversationId, tenantId, userId, upToSequenceNumber, traceId } = event;

    // 1. Load conversation from DB
    const conv = await this.conversationRepo.getConversation(tenantId, userId, conversationId);
    if (!conv) {
      this.logger.warn(`[ConversationSummaryService] Conversation ${conversationId} not found, skipping summary.`);
      return;
    }

    // 2. Watermark Idempotency Check (PATCH 3)
    if ((conv.summaryUpToSequence || 0) >= upToSequenceNumber) {
      this.logger.log(`[Observability] assistant_summary_skipped_duplicate: Conv ${conversationId} already summarized up to seq ${conv.summaryUpToSequence} >= requested ${upToSequenceNumber}`);
      return;
    }

    // 3. Load bounded messages up to requested sequence watermark (PATCH 3 & 6)
    const messages = await this.conversationRepo.getRecentMessages(
      tenantId,
      userId,
      conversationId,
      30,
      upToSequenceNumber,
    );

    if (messages.length === 0) {
      return;
    }

    // 4. Build summary prompt
    const prompt = this.promptBuilder.buildSummaryPrompt(conv.summary, messages);

    // 5. Call AiGovernance with dedicated capability 'assistant.summarize' (PATCH 4)
    const context: CurrentUser = {
      tenantId,
      userId,
      actorType: conv.actorType || ActorType.STAFF,
      roles: [conv.actorType || 'STAFF'],
      permissions: [],
      traceId,
    };

    try {
      const aiRes = await this.aiGovernanceClient.generate('assistant.summarize', prompt, context, 256);
      if (aiRes.content && aiRes.content.trim()) {
        const newSummary = aiRes.content.trim();

        // 6. Watermark update (PATCH 3)
        const updated = await this.conversationRepo.updateSummaryWatermark(
          tenantId,
          userId,
          conversationId,
          newSummary,
          upToSequenceNumber,
        );

        if (updated) {
          this.logger.log(`[Observability] assistant_summary_completed for conversation ${conversationId} up to seq ${upToSequenceNumber}`);
        } else {
          this.logger.log(`[Observability] assistant_summary_skipped_duplicate: Newer summary watermark already persisted for conv ${conversationId}`);
        }
      }
    } catch (err) {
      this.logger.error(`[Observability] assistant_summary_failed for conv ${conversationId}: ${(err as Error).message}`);
    }
  }
}
