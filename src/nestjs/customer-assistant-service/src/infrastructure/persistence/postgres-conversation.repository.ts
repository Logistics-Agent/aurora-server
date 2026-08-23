import { Injectable, Logger, OnModuleInit, OnModuleDestroy } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { Pool } from 'pg';
import { Conversation } from '../../domain/entities/conversation.entity';
import { ConversationMessage } from '../../domain/entities/message.entity';
import { IConversationRepository } from '../../domain/repositories/conversation.repository.interface';
import { ActorType } from '../../domain/enums/actor-type.enum';
import {
  ConversationConcurrencyConflictException,
  ConversationNotFoundException,
} from '../../domain/errors/assistant.errors';
import { RedisConversationCacheService } from './redis-conversation-cache.service';
import { InMemoryConversationStore } from './in-memory-conversation.store';

@Injectable()
export class PostgresConversationRepository
  implements IConversationRepository, OnModuleInit, OnModuleDestroy
{
  private readonly logger = new Logger(PostgresConversationRepository.name);
  private pool: Pool | null = null;
  private isConnected = false;
  private readonly fallbackMemoryStore = new InMemoryConversationStore();
  private readonly isProduction: boolean;

  constructor(
    private readonly configService: ConfigService,
    private readonly cacheService: RedisConversationCacheService,
  ) {
    this.isProduction = (this.configService.get<string>('NODE_ENV') || 'development') === 'production';
  }

  async onModuleInit() {
    const connectionString =
      this.configService.get<string>('DATABASE_URL') ||
      this.configService.get<string>('POSTGRES_URL');

    const host = this.configService.get<string>('POSTGRES_HOST') || 'localhost';
    const port = Number(this.configService.get<number>('POSTGRES_PORT') || 5432);
    const database = this.configService.get<string>('POSTGRES_DB') || 'aurora_customer_assistant';
    const user = this.configService.get<string>('POSTGRES_USER') || 'postgres';
    const password = this.configService.get<string>('POSTGRES_PASSWORD') || 'postgres';

    try {
      this.pool = new Pool(
        connectionString
          ? { connectionString, connectionTimeoutMillis: 3000 }
          : { host, port, database, user, password, connectionTimeoutMillis: 3000 },
      );

      const client = await this.pool.connect();
      try {
        await client.query('SELECT 1');
        this.isConnected = true;
        this.logger.log(`[PostgresRepo] Successfully connected to PostgreSQL.`);
      } finally {
        client.release();
      }
    } catch (err) {
      this.isConnected = false;
      if (this.isProduction) {
        this.logger.error(`[PostgresRepo] FATAL: PostgreSQL connection failed in PRODUCTION: ${(err as Error).message}`);
        throw new Error(`PostgreSQL database connection failed in production environment: ${(err as Error).message}`);
      } else {
        this.logger.warn(`[PostgresRepo] PostgreSQL not reachable in development (${(err as Error).message}). Using in-memory fallback.`);
      }
    }
  }

  async onModuleDestroy() {
    if (this.pool) {
      try {
        await this.pool.end();
      } catch {
        // ignore
      }
    }
  }

  async createConversation(conversation: Conversation): Promise<Conversation> {
    const initialVersion = conversation.version || 1;
    const initialWatermark = conversation.summaryUpToSequence || 0;
    conversation.version = initialVersion;
    conversation.summaryUpToSequence = initialWatermark;

    if (!this.isConnected || !this.pool) {
      return this.fallbackMemoryStore.createConversation(conversation);
    }

    try {
      await this.pool.query(
        `INSERT INTO conversations (id, tenant_id, user_id, actor_type, preferred_language, status, summary, summary_up_to_sequence, version, created_at, updated_at, last_activity_at)
         VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)`,
        [
          conversation.id,
          conversation.tenantId,
          conversation.userId,
          conversation.actorType,
          conversation.preferredLanguage,
          conversation.status,
          conversation.summary || null,
          initialWatermark,
          initialVersion,
          conversation.createdAt,
          conversation.updatedAt,
          conversation.lastActivityAt,
        ],
      );

      return conversation;
    } catch (err) {
      this.logger.error(`[PostgresRepo] Failed to create conversation: ${err}`);
      if (this.isProduction) throw err;
      return this.fallbackMemoryStore.createConversation(conversation);
    }
  }

  async getConversation(
    tenantId: string,
    userId: string,
    conversationId: string,
  ): Promise<Conversation | null> {
    if (!this.isConnected || !this.pool) {
      return this.fallbackMemoryStore.getConversation(tenantId, userId, conversationId);
    }

    try {
      const res = await this.pool.query(
        `SELECT id, tenant_id, user_id, actor_type, preferred_language, status, summary, summary_up_to_sequence, version, created_at, updated_at, last_activity_at
         FROM conversations
         WHERE id = $1 AND tenant_id = $2 AND user_id = $3`,
        [conversationId, tenantId, userId],
      );

      if (res.rows.length === 0) return null;
      const row = res.rows[0];

      return {
        id: row.id,
        tenantId: row.tenant_id,
        userId: row.user_id,
        actorType: row.actor_type as ActorType,
        preferredLanguage: row.preferred_language,
        status: row.status,
        summary: row.summary || undefined,
        summaryUpToSequence: Number(row.summary_up_to_sequence || 0),
        version: Number(row.version || 1),
        createdAt: new Date(row.created_at),
        updatedAt: new Date(row.updated_at),
        lastActivityAt: new Date(row.last_activity_at),
      };
    } catch (err) {
      this.logger.error(`[PostgresRepo] Failed to get conversation: ${err}`);
      if (this.isProduction) throw err;
      return this.fallbackMemoryStore.getConversation(tenantId, userId, conversationId);
    }
  }

  async listConversations(
    tenantId: string,
    userId: string,
    limit = 20,
  ): Promise<Conversation[]> {
    if (!this.isConnected || !this.pool) {
      return this.fallbackMemoryStore.listConversations(tenantId, userId, limit);
    }

    try {
      const res = await this.pool.query(
        `SELECT id, tenant_id, user_id, actor_type, preferred_language, status, summary, summary_up_to_sequence, version, created_at, updated_at, last_activity_at
         FROM conversations
         WHERE tenant_id = $1 AND user_id = $2
         ORDER BY last_activity_at DESC
         LIMIT $3`,
        [tenantId, userId, limit],
      );

      return res.rows.map((row) => ({
        id: row.id,
        tenantId: row.tenant_id,
        userId: row.user_id,
        actorType: row.actor_type as ActorType,
        preferredLanguage: row.preferred_language,
        status: row.status,
        summary: row.summary || undefined,
        summaryUpToSequence: Number(row.summary_up_to_sequence || 0),
        version: Number(row.version || 1),
        createdAt: new Date(row.created_at),
        updatedAt: new Date(row.updated_at),
        lastActivityAt: new Date(row.last_activity_at),
      }));
    } catch (err) {
      this.logger.error(`[PostgresRepo] Failed to list conversations: ${err}`);
      if (this.isProduction) throw err;
      return this.fallbackMemoryStore.listConversations(tenantId, userId, limit);
    }
  }

  async updateConversation(conversation: Conversation, expectedVersion?: number): Promise<void> {
    if (!this.isConnected || !this.pool) {
      return this.fallbackMemoryStore.updateConversation(conversation, expectedVersion);
    }

    try {
      const checkVersion = expectedVersion !== undefined ? expectedVersion : conversation.version;
      const res = await this.pool.query(
        `UPDATE conversations
         SET status = $1, summary = $2, summary_up_to_sequence = $3, version = version + 1, updated_at = $4, last_activity_at = $5
         WHERE id = $6 AND tenant_id = $7 AND user_id = $8 AND version = $9`,
        [
          conversation.status,
          conversation.summary || null,
          conversation.summaryUpToSequence || 0,
          conversation.updatedAt,
          conversation.lastActivityAt,
          conversation.id,
          conversation.tenantId,
          conversation.userId,
          checkVersion,
        ],
      );

      if (res.rowCount === 0) {
        const existing = await this.getConversation(conversation.tenantId, conversation.userId, conversation.id);
        if (!existing) {
          throw new ConversationNotFoundException(conversation.id);
        }
        throw new ConversationConcurrencyConflictException(conversation.id, existing.version, checkVersion);
      }

      conversation.version = checkVersion + 1;
    } catch (err) {
      this.logger.error(`[PostgresRepo] Failed to update conversation: ${err}`);
      throw err;
    }
  }

  async updateSummaryWatermark(
    tenantId: string,
    userId: string,
    conversationId: string,
    summary: string,
    upToSequenceNumber: number,
  ): Promise<boolean> {
    if (!this.isConnected || !this.pool) {
      return this.fallbackMemoryStore.updateSummaryWatermark(
        tenantId,
        userId,
        conversationId,
        summary,
        upToSequenceNumber,
      );
    }

    try {
      const res = await this.pool.query(
        `UPDATE conversations
         SET summary = $1,
             summary_up_to_sequence = $2,
             updated_at = NOW()
         WHERE id = $3
           AND tenant_id = $4
           AND user_id = $5
           AND summary_up_to_sequence < $2`,
        [summary, upToSequenceNumber, conversationId, tenantId, userId],
      );

      return (res.rowCount ?? 0) > 0;
    } catch (err) {
      this.logger.error(`[PostgresRepo] Failed to update summary watermark: ${err}`);
      if (this.isProduction) throw err;
      return this.fallbackMemoryStore.updateSummaryWatermark(
        tenantId,
        userId,
        conversationId,
        summary,
        upToSequenceNumber,
      );
    }
  }

  async appendMessage(
    tenantId: string,
    userId: string,
    message: ConversationMessage,
  ): Promise<ConversationMessage> {
    await this.cacheService.invalidateRecentMessages(tenantId, userId, message.conversationId);

    if (!this.isConnected || !this.pool) {
      return this.fallbackMemoryStore.appendMessage(tenantId, userId, message);
    }

    const client = await this.pool.connect();
    try {
      await client.query('BEGIN');

      // 1. Lock conversation row in transaction for atomic sequence allocation (PATCH 1)
      const lockRes = await client.query(
        `SELECT id
         FROM conversations
         WHERE id = $1 AND tenant_id = $2 AND user_id = $3
         FOR UPDATE`,
        [message.conversationId, tenantId, userId],
      );

      if (lockRes.rows.length === 0) {
        throw new ConversationNotFoundException(message.conversationId);
      }

      // 2. Deterministic sequence allocation under row lock
      const seqRes = await client.query(
        `SELECT COALESCE(MAX(sequence_number), 0) + 1 AS next_seq
         FROM conversation_messages
         WHERE conversation_id = $1`,
        [message.conversationId],
      );

      const sequenceNumber = Number(seqRes.rows[0]?.next_seq || 1);
      message.sequenceNumber = sequenceNumber;

      // 3. Insert message
      await client.query(
        `INSERT INTO conversation_messages (
           id, conversation_id, sequence_number, role, content, intent, sources_json, conflicts_json, insufficient_evidence, retrieval_trace_id, ai_decision_id, metadata, created_at
         ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13)`,
        [
          message.id,
          message.conversationId,
          sequenceNumber,
          message.role,
          message.content,
          message.intent || null,
          message.sources ? JSON.stringify(message.sources) : null,
          message.conflicts ? JSON.stringify(message.conflicts) : null,
          message.insufficientEvidence || false,
          message.retrievalTraceId || null,
          message.aiDecisionId || null,
          message.metadata ? JSON.stringify(message.metadata) : null,
          message.createdAt,
        ],
      );

      // 4. Update conversation timestamp
      await client.query(
        `UPDATE conversations
         SET last_activity_at = $1, updated_at = $1
         WHERE id = $2 AND tenant_id = $3 AND user_id = $4`,
        [message.createdAt, message.conversationId, tenantId, userId],
      );

      await client.query('COMMIT');
      return message;
    } catch (err) {
      await client.query('ROLLBACK');
      this.logger.error(`[PostgresRepo] Transaction failed in appendMessage: ${err}`);
      if (this.isProduction) throw err;
      return this.fallbackMemoryStore.appendMessage(tenantId, userId, message);
    } finally {
      client.release();
    }
  }

  async getRecentMessages(
    tenantId: string,
    userId: string,
    conversationId: string,
    limit = 10,
    upToSequenceNumber?: number,
  ): Promise<ConversationMessage[]> {
    if (upToSequenceNumber === undefined) {
      const cached = await this.cacheService.getRecentMessages(tenantId, userId, conversationId);
      if (cached && cached.length >= limit) {
        return cached.slice(-limit);
      }
    }

    if (!this.isConnected || !this.pool) {
      return this.fallbackMemoryStore.getRecentMessages(
        tenantId,
        userId,
        conversationId,
        limit,
        upToSequenceNumber,
      );
    }

    try {
      const res = await this.pool.query(
        `SELECT id, conversation_id, sequence_number, role, content, intent, sources_json, conflicts_json, insufficient_evidence, retrieval_trace_id, ai_decision_id, metadata, created_at
         FROM conversation_messages
         WHERE conversation_id = $1
           AND ($2::int IS NULL OR sequence_number <= $2)
         ORDER BY sequence_number DESC
         LIMIT $3`,
        [conversationId, upToSequenceNumber || null, limit],
      );

      const messages: ConversationMessage[] = res.rows
        .reverse()
        .map((row) => ({
          id: row.id,
          conversationId: row.conversation_id,
          sequenceNumber: Number(row.sequence_number),
          role: row.role,
          content: row.content,
          intent: row.intent,
          sources: row.sources_json || undefined,
          conflicts: row.conflicts_json || undefined,
          insufficientEvidence: Boolean(row.insufficient_evidence),
          retrievalTraceId: row.retrieval_trace_id || undefined,
          aiDecisionId: row.ai_decision_id || undefined,
          metadata: row.metadata || undefined,
          createdAt: new Date(row.created_at),
        }));

      if (upToSequenceNumber === undefined) {
        await this.cacheService.setRecentMessages(tenantId, userId, conversationId, messages);
      }

      return messages;
    } catch (err) {
      this.logger.error(`[PostgresRepo] Failed to get recent messages: ${err}`);
      if (this.isProduction) throw err;
      return this.fallbackMemoryStore.getRecentMessages(
        tenantId,
        userId,
        conversationId,
        limit,
        upToSequenceNumber,
      );
    }
  }

  async getUnsummarizedMessages(
    tenantId: string,
    userId: string,
    conversationId: string,
    afterSequenceNumber: number,
    upToSequenceNumber?: number,
    limit = 20,
  ): Promise<ConversationMessage[]> {
    if (!this.isConnected || !this.pool) {
      return this.fallbackMemoryStore.getUnsummarizedMessages(
        tenantId,
        userId,
        conversationId,
        afterSequenceNumber,
        upToSequenceNumber,
        limit,
      );
    }

    try {
      const res = await this.pool.query(
        `SELECT id, conversation_id, sequence_number, role, content, intent, sources_json, conflicts_json, insufficient_evidence, retrieval_trace_id, ai_decision_id, metadata, created_at
         FROM conversation_messages
         WHERE conversation_id = $1
           AND sequence_number > $2
           AND ($3::int IS NULL OR sequence_number <= $3)
         ORDER BY sequence_number ASC
         LIMIT $4`,
        [conversationId, afterSequenceNumber, upToSequenceNumber || null, limit],
      );

      return res.rows.map((row) => ({
        id: row.id,
        conversationId: row.conversation_id,
        sequenceNumber: Number(row.sequence_number),
        role: row.role,
        content: row.content,
        intent: row.intent,
        sources: row.sources_json || undefined,
        conflicts: row.conflicts_json || undefined,
        insufficientEvidence: Boolean(row.insufficient_evidence),
        retrievalTraceId: row.retrieval_trace_id || undefined,
        aiDecisionId: row.ai_decision_id || undefined,
        metadata: row.metadata || undefined,
        createdAt: new Date(row.created_at),
      }));
    } catch (err) {
      this.logger.error(`[PostgresRepo] Failed to get unsummarized messages: ${err}`);
      if (this.isProduction) throw err;
      return this.fallbackMemoryStore.getUnsummarizedMessages(
        tenantId,
        userId,
        conversationId,
        afterSequenceNumber,
        upToSequenceNumber,
        limit,
      );
    }
  }
}
