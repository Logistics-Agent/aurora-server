import { Injectable, Logger, Inject } from '@nestjs/common';
import { randomUUID } from 'crypto';
import {
  IConversationRepository,
  CONVERSATION_REPOSITORY,
} from '../../domain/repositories/conversation.repository.interface';
import { Conversation } from '../../domain/entities/conversation.entity';
import {
  ConversationMessage,
  ConversationMessageMetadata,
  RegulatoryCitationInfo,
  KnowledgeReferenceInfo,
  GroundedConflictInfo,
} from '../../domain/entities/message.entity';
import { AssistantIntent } from '../../domain/enums/assistant-intent.enum';
import { NeedRagDecision } from '../../domain/enums/need-rag-decision.enum';
import { CurrentUser } from '../../infrastructure/security/current-user.interface';
import { IntentRouterService } from '../intent/intent-router.service';
import { ToolRegistryService } from '../tools/tool-registry.service';
import { ConversationalPromptBuilder } from '../prompt/conversational-prompt-builder';
import { AiGovernanceGrpcClient } from '../../infrastructure/grpc/ai-governance.grpc-client';
import { RegulatoryComplianceGrpcClient } from '../../infrastructure/grpc/regulatory-compliance.grpc-client';
import { ConversationSummaryService } from '../summary/conversation-summary.service';
import { AssistantCorpusAccessPolicy } from '../policy/assistant-corpus-access.policy';
import {
  ConversationNotFoundException,
  ConversationAccessDeniedException,
} from '../../domain/errors/assistant.errors';

export interface ProcessMessageResult {
  messageId: string;
  conversationId: string;
  sequenceNumber: number;
  role: string;
  answer: string;
  intent: AssistantIntent;
  decision: NeedRagDecision;
  sources: {
    regulatory: RegulatoryCitationInfo[];
    knowledge: KnowledgeReferenceInfo[];
  };
  conflicts: GroundedConflictInfo[];
  insufficientEvidence: boolean;
  governance?: {
    decisionId: string;
    automationLevel: string;
    capabilityCode: string;
  };
  metadata?: ConversationMessageMetadata;
  timestamp: string;
}

@Injectable()
export class ConversationalAssistantOrchestrator {
  private readonly logger = new Logger(ConversationalAssistantOrchestrator.name);
  private readonly maxHistoryTurns = 6; // 12 messages in active window
  private readonly summaryTriggerThreshold = 10; // Trigger rolling summary when unsummarized turns >= 10

  constructor(
    @Inject(CONVERSATION_REPOSITORY)
    private readonly conversationRepo: IConversationRepository,
    private readonly intentRouter: IntentRouterService,
    private readonly toolRegistry: ToolRegistryService,
    private readonly promptBuilder: ConversationalPromptBuilder,
    private readonly aiGovernanceClient: AiGovernanceGrpcClient,
    private readonly complianceClient: RegulatoryComplianceGrpcClient,
    private readonly summaryService: ConversationSummaryService,
    private readonly accessPolicy: AssistantCorpusAccessPolicy,
  ) {}

  async createConversation(
    currentUser: CurrentUser,
    preferredLanguage = 'vi',
  ): Promise<Conversation> {
    const conversation: Conversation = {
      id: randomUUID(),
      tenantId: currentUser.tenantId,
      userId: currentUser.userId,
      actorType: currentUser.actorType,
      preferredLanguage,
      status: 'ACTIVE',
      summaryUpToSequence: 0,
      version: 1,
      createdAt: new Date(),
      updatedAt: new Date(),
      lastActivityAt: new Date(),
    };

    return this.conversationRepo.createConversation(conversation);
  }

  async getConversation(
    conversationId: string,
    currentUser: CurrentUser,
  ): Promise<{ conversation: Conversation; messages: ConversationMessage[] }> {
    const conv = await this.conversationRepo.getConversation(
      currentUser.tenantId,
      currentUser.userId,
      conversationId,
    );

    if (!conv) {
      throw new ConversationNotFoundException(conversationId);
    }

    if (conv.tenantId !== currentUser.tenantId) {
      throw new ConversationAccessDeniedException(conversationId);
    }

    const messages = await this.conversationRepo.getRecentMessages(
      currentUser.tenantId,
      currentUser.userId,
      conversationId,
      30,
    );

    return { conversation: conv, messages };
  }

  async listConversations(currentUser: CurrentUser): Promise<Conversation[]> {
    return this.conversationRepo.listConversations(currentUser.tenantId, currentUser.userId);
  }

  async processMessage(
    conversationId: string,
    messageText: string,
    currentUser: CurrentUser,
  ): Promise<ProcessMessageResult> {
    const startTime = Date.now();
    const trimmedMessage = messageText.trim();
    if (!trimmedMessage) {
      throw new Error('Message content cannot be empty.');
    }

    // 1. Verify Conversation Existence and Tenant/User Ownership
    let conv = await this.conversationRepo.getConversation(
      currentUser.tenantId,
      currentUser.userId,
      conversationId,
    );

    if (!conv) {
      conv = await this.createConversation(currentUser);
      conversationId = conv.id;
    }

    // 2. Append User Message with Deterministic Sequence under Row Lock (PATCH 1 & 6)
    const userMsgId = randomUUID();
    const userMessage: ConversationMessage = {
      id: userMsgId,
      conversationId,
      role: 'USER',
      content: trimmedMessage,
      createdAt: new Date(),
    };
    const savedUserMsg = await this.conversationRepo.appendMessage(
      currentUser.tenantId,
      currentUser.userId,
      userMessage,
    );
    const inputSequenceNumber = savedUserMsg.sequenceNumber || 1;

    // 3. Prompt Context Reconstruction Using Summary Watermark (PATCH 7)
    let recentMessages: ConversationMessage[] = [];
    const summaryWatermark = conv.summaryUpToSequence || 0;

    if (conv.summary && summaryWatermark > 0) {
      // Load unsummarized turns: sequence > summaryWatermark AND sequence <= inputSequenceNumber
      recentMessages = await this.conversationRepo.getUnsummarizedMessages(
        currentUser.tenantId,
        currentUser.userId,
        conversationId,
        summaryWatermark,
        inputSequenceNumber,
        this.maxHistoryTurns * 2,
      );
    } else {
      // Standard bounded recent history up to inputSequenceNumber
      recentMessages = await this.conversationRepo.getRecentMessages(
        currentUser.tenantId,
        currentUser.userId,
        conversationId,
        this.maxHistoryTurns * 2,
        inputSequenceNumber,
      );
    }

    // 4. Summary Trigger Based on Unsummarized Count (PATCH 8)
    const unsummarizedCount = inputSequenceNumber - summaryWatermark;
    if (unsummarizedCount >= this.summaryTriggerThreshold) {
      await this.summaryService.enqueueSummaryJob({
        conversationId,
        tenantId: currentUser.tenantId,
        userId: currentUser.userId,
        upToSequenceNumber: inputSequenceNumber,
        traceId: currentUser.traceId,
      });
    }

    // 5. Layered Intent Classification
    const classification = await this.intentRouter.classify(trimmedMessage, currentUser);
    this.logger.log(
      `[Orchestrator] Conv: ${conversationId} | Seq: ${inputSequenceNumber} | Intent: ${classification.intent} | Decision: ${classification.decision}`,
    );

    let answerText = '';
    let regulatorySources: RegulatoryCitationInfo[] = [];
    let knowledgeSources: KnowledgeReferenceInfo[] = [];
    let conflicts: GroundedConflictInfo[] = [];
    let isInsufficient = false;
    let capabilityCode = 'assistant.general';
    let governanceInfo = {
      decisionId: 'none',
      automationLevel: 'ASSISTED',
      capabilityCode,
    };
    const toolCallsMetadata: Array<{ toolName: string; outcome: 'SUCCESS' | 'FAILED' | 'DENIED'; durationMs?: number }> = [];

    // 6. Synthesis & Routing Matrix (PATCH 2, 6, 7 & 11)
    switch (classification.decision) {
      case NeedRagDecision.NO_RAG: {
        capabilityCode = 'assistant.general';
        const prompt = this.promptBuilder.buildPrompt({
          userQuery: trimmedMessage,
          recentMessages,
          conversationSummary: conv.summary,
          userLanguage: conv.preferredLanguage,
        });

        const aiRes = await this.aiGovernanceClient.generate(capabilityCode, prompt, currentUser);
        answerText = aiRes.content;
        governanceInfo = {
          decisionId: aiRes.decisionId,
          automationLevel: aiRes.automationLevel,
          capabilityCode,
        };
        break;
      }

      case NeedRagDecision.DOMAIN_QUERY: {
        capabilityCode = 'assistant.general';
        let toolSummary = '';
        const toolStart = Date.now();

        if (classification.intent === AssistantIntent.SHIPMENT_QUERY) {
          try {
            const toolRes = await this.toolRegistry.executeTool(
              'ShipmentLookupTool',
              { currentUser, conversationId },
              classification.extractedParameters,
            );
            toolSummary = toolRes.summary;
            toolCallsMetadata.push({ toolName: 'ShipmentLookupTool', outcome: toolRes.success ? 'SUCCESS' : 'FAILED', durationMs: Date.now() - toolStart });
          } catch (e) {
            toolCallsMetadata.push({ toolName: 'ShipmentLookupTool', outcome: 'DENIED', durationMs: Date.now() - toolStart });
            toolSummary = 'Không có quyền truy cập dữ liệu lô hàng.';
          }
        } else if (classification.intent === AssistantIntent.BILLING_QUERY) {
          try {
            const toolRes = await this.toolRegistry.executeTool('BillingSummaryTool', {
              currentUser,
              conversationId,
            });
            toolSummary = toolRes.summary;
            toolCallsMetadata.push({ toolName: 'BillingSummaryTool', outcome: toolRes.success ? 'SUCCESS' : 'FAILED', durationMs: Date.now() - toolStart });
          } catch (e) {
            toolCallsMetadata.push({ toolName: 'BillingSummaryTool', outcome: 'DENIED', durationMs: Date.now() - toolStart });
            toolSummary = 'Không có quyền truy cập thông tin công nợ.';
          }
        }

        const prompt = this.promptBuilder.buildPrompt({
          userQuery: trimmedMessage,
          recentMessages,
          conversationSummary: conv.summary,
          toolSummary,
          userLanguage: conv.preferredLanguage,
        });

        const aiRes = await this.aiGovernanceClient.generate(capabilityCode, prompt, currentUser);
        answerText = aiRes.content;
        governanceInfo = {
          decisionId: aiRes.decisionId,
          automationLevel: aiRes.automationLevel,
          capabilityCode,
        };
        break;
      }

      case NeedRagDecision.RAG_KNOWLEDGE: {
        // Pure Knowledge RAG (capability: knowledge.answer)
        capabilityCode = 'knowledge.answer';
        const access = this.accessPolicy.canSearchKnowledge(currentUser.actorType, currentUser.tenantId, [], currentUser);
        if (!access.allowed) {
          answerText = access.reason || 'Không có quyền truy cập tri thức nội bộ.';
          isInsufficient = true;
          break;
        }

        const knowEvidence = await this.complianceClient.queryKnowledge(
          trimmedMessage,
          access.effectiveCategories || [],
          5,
          0.4,
          currentUser,
        );

        if (knowEvidence.length === 0) {
          answerText = 'Không tìm thấy quy trình SOP hoặc tài liệu nội bộ nào phù hợp với yêu cầu của quý khách.';
          isInsufficient = true;
          break;
        }

        const groundedRes = await this.synthesizeWithValidationRetry(
          capabilityCode,
          trimmedMessage,
          recentMessages,
          conv.summary,
          conv.preferredLanguage,
          [],
          knowEvidence,
          currentUser,
        );

        answerText = groundedRes.answer;
        knowledgeSources = groundedRes.knowledgeReferences;
        conflicts = groundedRes.conflicts;
        isInsufficient = groundedRes.insufficientEvidence;
        governanceInfo = {
          decisionId: groundedRes.governance.decisionId,
          automationLevel: groundedRes.governance.automationLevel,
          capabilityCode,
        };
        break;
      }

      case NeedRagDecision.RAG_REGULATORY: {
        // Pure Regulatory RAG (capability: compliance.answer)
        capabilityCode = 'compliance.answer';
        const access = this.accessPolicy.canSearchRegulatory(currentUser.actorType, currentUser.tenantId, '', currentUser);
        if (!access.allowed) {
          answerText = access.reason || 'Không có quyền tra cứu văn bản pháp luật.';
          isInsufficient = true;
          break;
        }

        const regEvidence = await this.complianceClient.queryRegulations(
          trimmedMessage,
          access.effectiveJurisdictions?.[0] || '',
          5,
          0.4,
          currentUser,
        );

        if (regEvidence.length === 0) {
          answerText = 'Không tìm thấy văn bản pháp luật hoặc quy định hải quan nào phù hợp với câu hỏi của quý khách.';
          isInsufficient = true;
          break;
        }

        const groundedRes = await this.synthesizeWithValidationRetry(
          capabilityCode,
          trimmedMessage,
          recentMessages,
          conv.summary,
          conv.preferredLanguage,
          regEvidence,
          [],
          currentUser,
        );

        answerText = groundedRes.answer;
        regulatorySources = groundedRes.regulatoryCitations;
        conflicts = groundedRes.conflicts;
        isInsufficient = groundedRes.insufficientEvidence;
        governanceInfo = {
          decisionId: groundedRes.governance.decisionId,
          automationLevel: groundedRes.governance.automationLevel,
          capabilityCode,
        };
        break;
      }

      case NeedRagDecision.RAG_HYBRID:
      default: {
        // Stage 3: CustomerAssistant owns HYBRID synthesis (capability: assistant.answer)
        capabilityCode = 'assistant.answer';
        const regAccess = this.accessPolicy.canSearchRegulatory(currentUser.actorType, currentUser.tenantId, '', currentUser);
        const knowAccess = this.accessPolicy.canSearchKnowledge(currentUser.actorType, currentUser.tenantId, [], currentUser);

        const [regEvidence, knowEvidence] = await Promise.all([
          regAccess.allowed
            ? this.complianceClient.queryRegulations(trimmedMessage, regAccess.effectiveJurisdictions?.[0] || '', 5, 0.4, currentUser)
            : Promise.resolve([]),
          knowAccess.allowed
            ? this.complianceClient.queryKnowledge(trimmedMessage, knowAccess.effectiveCategories || [], 5, 0.4, currentUser)
            : Promise.resolve([]),
        ]);

        if (regEvidence.length === 0 && knowEvidence.length === 0) {
          answerText = 'Không tìm thấy quy định pháp luật hoặc tài liệu nghiệp vụ công ty phù hợp với câu hỏi của quý khách.';
          isInsufficient = true;
          break;
        }

        const groundedRes = await this.synthesizeWithValidationRetry(
          capabilityCode,
          trimmedMessage,
          recentMessages,
          conv.summary,
          conv.preferredLanguage,
          regEvidence,
          knowEvidence,
          currentUser,
        );

        answerText = groundedRes.answer;
        regulatorySources = groundedRes.regulatoryCitations;
        knowledgeSources = groundedRes.knowledgeReferences;
        conflicts = groundedRes.conflicts;
        isInsufficient = groundedRes.insufficientEvidence;
        governanceInfo = {
          decisionId: groundedRes.governance.decisionId,
          automationLevel: groundedRes.governance.automationLevel,
          capabilityCode,
        };
        break;
      }
    }

    // 7. Save Assistant Message with Deterministic Sequence & Typed Metadata (PATCH 1, 6 & 10)
    const durationMs = Date.now() - startTime;
    const assistantMsgId = randomUUID();
    const msgMetadata: ConversationMessageMetadata = {
      capabilityCode,
      governanceDecisionId: governanceInfo.decisionId,
      toolCalls: toolCallsMetadata.length > 0 ? toolCallsMetadata : undefined,
      latencyMs: durationMs,
      promptVersion: '2.0',
      schemaVersion: '1.0',
      inputSequenceNumber,
    };

    const assistantMessage: ConversationMessage = {
      id: assistantMsgId,
      conversationId,
      role: 'ASSISTANT',
      content: answerText,
      intent: classification.intent,
      sources: {
        regulatory: regulatorySources,
        knowledge: knowledgeSources,
      },
      conflicts,
      insufficientEvidence: isInsufficient,
      aiDecisionId: governanceInfo.decisionId,
      metadata: msgMetadata,
      createdAt: new Date(),
    };

    const savedAssistantMsg = await this.conversationRepo.appendMessage(
      currentUser.tenantId,
      currentUser.userId,
      assistantMessage,
    );
    const outputSequenceNumber = savedAssistantMsg.sequenceNumber || inputSequenceNumber + 1;

    return {
      messageId: assistantMsgId,
      conversationId,
      sequenceNumber: outputSequenceNumber,
      role: 'ASSISTANT',
      answer: answerText,
      intent: classification.intent,
      decision: classification.decision,
      sources: {
        regulatory: regulatorySources,
        knowledge: knowledgeSources,
      },
      conflicts,
      insufficientEvidence: isInsufficient,
      governance: governanceInfo,
      metadata: { ...msgMetadata, outputSequenceNumber },
      timestamp: new Date().toISOString(),
    };
  }

  private async synthesizeWithValidationRetry(
    capabilityCode: string,
    userQuery: string,
    recentMessages: ConversationMessage[],
    conversationSummary: string | undefined,
    userLanguage: string,
    regEvidence: RegulatoryCitationInfo[],
    knowEvidence: KnowledgeReferenceInfo[],
    currentUser: CurrentUser,
  ): Promise<{
    answer: string;
    regulatoryCitations: RegulatoryCitationInfo[];
    knowledgeReferences: KnowledgeReferenceInfo[];
    conflicts: GroundedConflictInfo[];
    insufficientEvidence: boolean;
    governance: { decisionId: string; automationLevel: string };
  }> {
    // Attempt 1: Standard structured synthesis
    const prompt = this.promptBuilder.buildPrompt({
      userQuery,
      recentMessages,
      conversationSummary,
      regulatoryEvidence: regEvidence,
      knowledgeEvidence: knowEvidence,
      userLanguage,
      requireStructuredJson: true,
    });

    const aiRes = await this.aiGovernanceClient.generate(capabilityCode, prompt, currentUser);
    const parsed1 = this.parseLlmJson(aiRes.content);

    const validation1 = await this.complianceClient.validateGroundedEvidence(
      {
        answer: parsed1.answer,
        citations: parsed1.citations,
        knowledgeReferences: parsed1.knowledgeReferences,
        conflicts: parsed1.conflicts,
        insufficientEvidence: parsed1.insufficientEvidence,
        missingInformation: parsed1.missingInformation,
        availableRegulatoryEvidence: regEvidence,
        availableKnowledgeEvidence: knowEvidence,
      },
      currentUser,
    );

    const hasInvalidCitations =
      (parsed1.citations.length > 0 && validation1.validatedRegulatoryCitations.length === 0 && regEvidence.length > 0) ||
      (parsed1.knowledgeReferences.length > 0 && validation1.validatedKnowledgeReferences.length === 0 && knowEvidence.length > 0);

    if (!hasInvalidCitations && !validation1.insufficientEvidence) {
      return {
        answer: validation1.sanitizedAnswer,
        regulatoryCitations: validation1.validatedRegulatoryCitations,
        knowledgeReferences: validation1.validatedKnowledgeReferences,
        conflicts: validation1.validatedConflicts,
        insufficientEvidence: validation1.insufficientEvidence,
        governance: { decisionId: aiRes.decisionId, automationLevel: aiRes.automationLevel },
      };
    }

    // Attempt 2: Bounded 1-Retry with correction guidance (PATCH 7 & Observability)
    this.logger.warn(`[Observability] assistant_grounding_validation_retry: Attempting 1 bounded correction retry.`);
    const retryPrompt = `${prompt}\n\nIMPORTANT CORRECTION: Your previous answer contained citations not matched in the provided evidence. Re-answer strictly citing only provided evidence IDs ([R1]..[Rn], [K1]..[Km]).`;

    try {
      const retryAiRes = await this.aiGovernanceClient.generate(capabilityCode, retryPrompt, currentUser);
      const parsed2 = this.parseLlmJson(retryAiRes.content);

      const validation2 = await this.complianceClient.validateGroundedEvidence(
        {
          answer: parsed2.answer,
          citations: parsed2.citations,
          knowledgeReferences: parsed2.knowledgeReferences,
          conflicts: parsed2.conflicts,
          insufficientEvidence: parsed2.insufficientEvidence,
          missingInformation: parsed2.missingInformation,
          availableRegulatoryEvidence: regEvidence,
          availableKnowledgeEvidence: knowEvidence,
        },
        currentUser,
      );

      return {
        answer: validation2.sanitizedAnswer,
        regulatoryCitations: validation2.validatedRegulatoryCitations,
        knowledgeReferences: validation2.validatedKnowledgeReferences,
        conflicts: validation2.validatedConflicts,
        insufficientEvidence: validation2.insufficientEvidence,
        governance: { decisionId: retryAiRes.decisionId, automationLevel: retryAiRes.automationLevel },
      };
    } catch {
      this.logger.error(`[Observability] assistant_grounding_validation_failed: Retry failed, returning safe fallback.`);
      return {
        answer: validation1.sanitizedAnswer || 'Căn cứ các tài liệu hiện có, thông tin chưa đủ để đưa ra kết luận chắc chắn.',
        regulatoryCitations: validation1.validatedRegulatoryCitations,
        knowledgeReferences: validation1.validatedKnowledgeReferences,
        conflicts: validation1.validatedConflicts,
        insufficientEvidence: true,
        governance: { decisionId: aiRes.decisionId, automationLevel: aiRes.automationLevel },
      };
    }
  }

  private parseLlmJson(rawContent: string): {
    answer: string;
    citations: Array<{ evidenceId: string }>;
    knowledgeReferences: Array<{ evidenceId: string }>;
    conflicts: Array<{ regulatoryEvidenceId: string; knowledgeEvidenceId: string; description: string }>;
    insufficientEvidence: boolean;
    missingInformation: string[];
  } {
    if (!rawContent || !rawContent.trim()) {
      return {
        answer: '',
        citations: [],
        knowledgeReferences: [],
        conflicts: [],
        insufficientEvidence: true,
        missingInformation: ['Empty response from model.'],
      };
    }

    let clean = rawContent.trim();
    if (clean.startsWith('```')) {
      clean = clean.replace(/```(?:json)?/gi, '').replace(/```/g, '').trim();
    }

    try {
      const parsed = JSON.parse(clean);
      return {
        answer: parsed.answer || clean,
        citations: parsed.citations || [],
        knowledgeReferences: parsed.knowledgeReferences || [],
        conflicts: parsed.conflicts || [],
        insufficientEvidence: Boolean(parsed.insufficientEvidence),
        missingInformation: parsed.missingInformation || [],
      };
    } catch {
      return {
        answer: rawContent,
        citations: [],
        knowledgeReferences: [],
        conflicts: [],
        insufficientEvidence: false,
        missingInformation: [],
      };
    }
  }
}
