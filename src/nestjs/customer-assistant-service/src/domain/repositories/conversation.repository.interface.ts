import { Conversation } from '../entities/conversation.entity';
import { ConversationMessage } from '../entities/message.entity';

export const CONVERSATION_REPOSITORY = Symbol('CONVERSATION_REPOSITORY');

export interface IConversationRepository {
  createConversation(conversation: Conversation): Promise<Conversation>;
  getConversation(tenantId: string, userId: string, conversationId: string): Promise<Conversation | null>;
  listConversations(tenantId: string, userId: string, limit?: number): Promise<Conversation[]>;
  updateConversation(conversation: Conversation, expectedVersion?: number): Promise<void>;
  updateSummaryWatermark(
    tenantId: string,
    userId: string,
    conversationId: string,
    summary: string,
    upToSequenceNumber: number,
  ): Promise<boolean>;
  appendMessage(tenantId: string, userId: string, message: ConversationMessage): Promise<ConversationMessage>;
  getRecentMessages(
    tenantId: string,
    userId: string,
    conversationId: string,
    limit?: number,
    upToSequenceNumber?: number,
  ): Promise<ConversationMessage[]>;
  getUnsummarizedMessages(
    tenantId: string,
    userId: string,
    conversationId: string,
    afterSequenceNumber: number,
    upToSequenceNumber?: number,
    limit?: number,
  ): Promise<ConversationMessage[]>;
}
