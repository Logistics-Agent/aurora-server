import { Injectable, Logger } from '@nestjs/common';
import { Conversation } from '../../domain/entities/conversation.entity';
import { ConversationMessage } from '../../domain/entities/message.entity';
import { IConversationRepository } from '../../domain/repositories/conversation.repository.interface';
import {
  ConversationConcurrencyConflictException,
  ConversationNotFoundException,
} from '../../domain/errors/assistant.errors';

@Injectable()
export class InMemoryConversationStore implements IConversationRepository {
  private readonly logger = new Logger(InMemoryConversationStore.name);

  // Key: `${tenantId}:${userId}:${conversationId}`
  private readonly conversations = new Map<string, Conversation>();

  // Key: `${tenantId}:${userId}:${conversationId}`, Value: ConversationMessage[]
  private readonly messages = new Map<string, ConversationMessage[]>();

  private buildKey(tenantId: string, userId: string, conversationId: string): string {
    return `${tenantId}:${userId}:${conversationId}`;
  }

  async createConversation(conversation: Conversation): Promise<Conversation> {
    conversation.version = conversation.version || 1;
    const key = this.buildKey(conversation.tenantId, conversation.userId, conversation.id);
    this.conversations.set(key, { ...conversation });
    this.messages.set(key, []);
    this.logger.debug(`[ConversationStore] Created conversation ${conversation.id} for user ${conversation.userId} (Tenant: ${conversation.tenantId})`);
    return conversation;
  }

  async getConversation(
    tenantId: string,
    userId: string,
    conversationId: string,
  ): Promise<Conversation | null> {
    const key = this.buildKey(tenantId, userId, conversationId);
    const conv = this.conversations.get(key);
    return conv ? { ...conv } : null;
  }

  async listConversations(
    tenantId: string,
    userId: string,
    limit = 20,
  ): Promise<Conversation[]> {
    const prefix = `${tenantId}:${userId}:`;
    const results: Conversation[] = [];

    for (const [key, conv] of this.conversations.entries()) {
      if (key.startsWith(prefix)) {
        results.push({ ...conv });
      }
    }

    return results
      .sort((a, b) => b.lastActivityAt.getTime() - a.lastActivityAt.getTime())
      .slice(0, limit);
  }

  async updateConversation(conversation: Conversation, expectedVersion?: number): Promise<void> {
    const key = this.buildKey(conversation.tenantId, conversation.userId, conversation.id);
    const existing = this.conversations.get(key);

    if (!existing) {
      throw new ConversationNotFoundException(conversation.id);
    }

    const checkVersion = expectedVersion !== undefined ? expectedVersion : conversation.version;
    if (existing.version !== checkVersion) {
      throw new ConversationConcurrencyConflictException(conversation.id, existing.version, checkVersion);
    }

    const updated = {
      ...conversation,
      version: checkVersion + 1,
      updatedAt: new Date(),
    };
    conversation.version = updated.version;
    this.conversations.set(key, updated);
  }

  async appendMessage(
    tenantId: string,
    userId: string,
    message: ConversationMessage,
  ): Promise<ConversationMessage> {
    const key = this.buildKey(tenantId, userId, message.conversationId);
    const list = this.messages.get(key) || [];
    const nextSeq = list.length + 1;

    const savedMessage: ConversationMessage = {
      ...message,
      sequenceNumber: nextSeq,
    };
    list.push(savedMessage);
    this.messages.set(key, list);

    const conv = this.conversations.get(key);
    if (conv) {
      conv.lastActivityAt = message.createdAt;
      conv.updatedAt = message.createdAt;
    }

    return savedMessage;
  }

  async getRecentMessages(
    tenantId: string,
    userId: string,
    conversationId: string,
    limit = 10,
    upToSequenceNumber?: number,
  ): Promise<ConversationMessage[]> {
    const key = this.buildKey(tenantId, userId, conversationId);
    const list = this.messages.get(key) || [];

    const filtered = upToSequenceNumber !== undefined
      ? list.filter((m) => (m.sequenceNumber || 0) <= upToSequenceNumber)
      : list;

    return filtered.slice(-limit).map((m) => ({ ...m }));
  }
}
