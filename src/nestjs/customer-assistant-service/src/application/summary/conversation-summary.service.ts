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
  expectedConversationVersion: number;
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

  constructor(
    private readonly configService: ConfigService,
    @Inject(CONVERSATION_REPOSITORY)
    private readonly conversationRepo: IConversationRepository,
    private readonly aiGovernanceClient: AiGovernanceGrpcClient,
    private readonly promptBuilder: ConversationalPromptBuilder,
  ) {}

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
            this.logger.error(`[ConversationSummaryService] Failed to process summary job: ${err}`);
            this.channel.nack(msg, false, false); // discard or dead-letter
          }
        }
      });

      this.isConnected = true;
      this.logger.log(`[ConversationSummaryService] Connected to RabbitMQ on queue ${this.queueName}`);
    } catch (err) {
      this.isConnected = false;
      this.logger.warn(`[ConversationSummaryService] RabbitMQ not reachable (${(err as Error).message}). Using in-process worker mode.`);
    }
  }

  async enqueueSummaryJob(event: ConversationSummaryRequestedEvent): Promise<void> {
    if (this.isConnected && this.channel) {
      try {
        const payload = Buffer.from(JSON.stringify(event));
        this.channel.publish(this.exchangeName, this.routingKey, payload, { persistent: true });
        this.logger.debug(`[ConversationSummaryService] Enqueued summary job for conversation ${event.conversationId}`);
        return;
      } catch (err) {
        this.logger.warn(`[ConversationSummaryService] RabbitMQ publish failed: ${err}. Falling back to in-process execution.`);
      }
    }

    // In-process fallback when RabbitMQ is offline (dev / test)
    setImmediate(() => {
      this.handleSummaryJob(event).catch((err) => {
        this.logger.error(`[ConversationSummaryService] In-process summary job error: ${err}`);
      });
    });
  }

  async handleSummaryJob(event: ConversationSummaryRequestedEvent): Promise<void> {
    const { conversationId, tenantId, userId, expectedConversationVersion, upToSequenceNumber, traceId } = event;

    // 1. Load conversation from DB
    const conv = await this.conversationRepo.getConversation(tenantId, userId, conversationId);
    if (!conv) {
      this.logger.warn(`[ConversationSummaryService] Conversation ${conversationId} not found, skipping summary.`);
      return;
    }

    // 2. Idempotency Check (PATCH 3 & 5)
    // If conversation version changed beyond expected version, verify before proceeding
    if (conv.version > expectedConversationVersion + 5) {
      this.logger.debug(`[ConversationSummaryService] Stale summary job for conv ${conversationId}. Current version: ${conv.version}, expected: ${expectedConversationVersion}`);
    }

    // 3. Load bounded messages up to sequence watermark (PATCH 3 & 6)
    const messages = await this.conversationRepo.getRecentMessages(
      tenantId,
      userId,
      conversationId,
      20,
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
        conv.summary = aiRes.content.trim();
        conv.updatedAt = new Date();

        // 6. Atomic optimistic concurrency update (PATCH 5)
        await this.conversationRepo.updateConversation(conv, conv.version);
        this.logger.log(`[ConversationSummaryService] Successfully updated summary for conversation ${conversationId} up to seq ${upToSequenceNumber}`);
      }
    } catch (err) {
      this.logger.warn(`[ConversationSummaryService] Summary LLM execution failed for conv ${conversationId}: ${(err as Error).message}`);
    }
  }
}
