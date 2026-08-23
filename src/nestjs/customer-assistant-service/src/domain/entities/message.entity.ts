import { AssistantIntent } from '../enums/assistant-intent.enum';

export type MessageRole = 'USER' | 'ASSISTANT' | 'SYSTEM_EVENT';

export interface GroundedConflictInfo {
  regulatoryEvidenceId: string;
  knowledgeEvidenceId: string;
  description: string;
}

export interface RegulatoryCitationInfo {
  evidenceId: string;
  sourceId: string;
  documentVersionId: string;
  chunkId: string;
  title: string;
  authority: string;
  jurisdiction: string;
  regulationType: string;
  section: string;
  page: string;
  excerpt: string;
  canonicalSourceUri: string;
  score: number;
}

export interface KnowledgeReferenceInfo {
  evidenceId: string;
  sourceId: string;
  documentVersionId: string;
  chunkId: string;
  title: string;
  category: string;
  section: string;
  page: string;
  excerpt: string;
  score: number;
}

export interface AssistantToolCallMetadata {
  toolName: string;
  outcome: 'SUCCESS' | 'FAILED' | 'DENIED';
  durationMs?: number;
}

export interface ConversationMessageMetadata {
  capabilityCode?: string;
  governanceDecisionId?: string;
  governanceExecutionId?: string;
  retrievalTraceIds?: string[];
  toolCalls?: AssistantToolCallMetadata[];
  latencyMs?: number;
  promptVersion?: string;
  schemaVersion?: string;
  inputSequenceNumber?: number;
  outputSequenceNumber?: number;
}

export interface ConversationMessage {
  id: string;
  conversationId: string;
  sequenceNumber?: number;
  role: MessageRole;
  content: string;
  intent?: AssistantIntent;
  sources?: {
    regulatory: RegulatoryCitationInfo[];
    knowledge: KnowledgeReferenceInfo[];
  };
  conflicts?: GroundedConflictInfo[];
  insufficientEvidence?: boolean;
  retrievalTraceId?: string;
  aiDecisionId?: string;
  metadata?: ConversationMessageMetadata;
  createdAt: Date;
}
