import { ConversationalAssistantOrchestrator } from './conversational-assistant.orchestrator';
import { InMemoryConversationStore } from '../../infrastructure/persistence/in-memory-conversation.store';
import { IntentRouterService } from '../intent/intent-router.service';
import { ToolRegistryService } from '../tools/tool-registry.service';
import { ShipmentLookupTool } from '../tools/shipment-lookup.tool';
import { BillingSummaryTool } from '../tools/billing-summary.tool';
import { RegulatorySearchTool } from '../tools/regulatory-search.tool';
import { KnowledgeSearchTool } from '../tools/knowledge-search.tool';
import { ReadModelStore } from '../../read-model/read-model.store';
import { ConversationalPromptBuilder } from '../prompt/conversational-prompt-builder';
import { ConversationSummaryService } from '../summary/conversation-summary.service';
import { AssistantCorpusAccessPolicy } from '../policy/assistant-corpus-access.policy';
import { CurrentUser } from '../../infrastructure/security/current-user.interface';
import { ActorType } from '../../domain/enums/actor-type.enum';

describe('ConversationalAssistantOrchestrator', () => {
  let orchestrator: ConversationalAssistantOrchestrator;
  let repo: InMemoryConversationStore;
  let mockAiGovernance: any;
  let mockComplianceClient: any;
  let mockSummaryService: any;
  let accessPolicy: AssistantCorpusAccessPolicy;

  const staffUser: CurrentUser = {
    tenantId: 'tenant-1',
    userId: 'user-1',
    customerId: 'CUST-001',
    actorType: ActorType.STAFF,
    roles: ['STAFF'],
    permissions: [],
  };

  const customerUser: CurrentUser = {
    tenantId: 'tenant-1',
    userId: 'cust-user-1',
    customerId: 'CUST-001',
    actorType: ActorType.CUSTOMER,
    roles: ['CUSTOMER'],
    permissions: [],
  };

  beforeEach(() => {
    repo = new InMemoryConversationStore();
    accessPolicy = new AssistantCorpusAccessPolicy();

    mockAiGovernance = {
      generate: jest.fn().mockImplementation((capCode: string) => {
        if (capCode === 'assistant.answer') {
          return Promise.resolve({
            content: JSON.stringify({
              answer: 'According to Malaysian law [R1] and SOP [K1]...',
              citations: [{ evidenceId: 'R1' }],
              knowledgeReferences: [{ evidenceId: 'K1' }],
              conflicts: [],
              insufficientEvidence: false,
              missingInformation: [],
            }),
            decisionId: 'dec-hybrid',
            automationLevel: 'ASSISTED',
            requiresApproval: false,
            inputTokens: 50,
            outputTokens: 30,
          });
        }

        return Promise.resolve({
          content: 'Hello! How can I help you today?',
          decisionId: 'dec-general',
          automationLevel: 'ASSISTED',
          requiresApproval: false,
          inputTokens: 20,
          outputTokens: 10,
        });
      }),
    };

    mockComplianceClient = {
      queryRegulations: jest.fn().mockResolvedValue([
        {
          evidenceId: 'R1',
          sourceId: 'src-1',
          documentVersionId: 'v-1',
          chunkId: 'c-1',
          title: 'Customs Law',
          authority: 'Royal Customs',
          jurisdiction: 'MY',
          regulationType: 'DangerousGoods',
          section: 'Sec 1',
          page: 'P1',
          excerpt: 'UN 38.3 test required',
          canonicalSourceUri: 'urn:law:my',
          score: 0.9,
        },
      ]),
      queryKnowledge: jest.fn().mockResolvedValue([
        {
          evidenceId: 'K1',
          sourceId: 'src-2',
          documentVersionId: 'v-2',
          chunkId: 'c-2',
          title: 'DG SOP',
          category: 'SOP',
          section: 'Sec 2',
          page: 'P2',
          excerpt: 'Warehouse packing check',
          score: 0.85,
        },
      ]),
      validateGroundedEvidence: jest.fn().mockImplementation((req: any) => {
        return Promise.resolve({
          sanitizedAnswer: req.answer,
          validatedRegulatoryCitations: req.availableRegulatoryEvidence,
          validatedKnowledgeReferences: req.availableKnowledgeEvidence,
          validatedConflicts: req.conflicts,
          insufficientEvidence: req.insufficientEvidence,
          missingInformation: req.missingInformation,
        });
      }),
    };

    mockSummaryService = {
      enqueueSummaryJob: jest.fn().mockResolvedValue(undefined),
    };

    const readModel = new ReadModelStore();
    const shipmentTool = new ShipmentLookupTool(readModel);
    const billingTool = new BillingSummaryTool(readModel);
    const regTool = new RegulatorySearchTool(mockComplianceClient, accessPolicy);
    const knowTool = new KnowledgeSearchTool(mockComplianceClient, accessPolicy);
    const toolRegistry = new ToolRegistryService(shipmentTool, billingTool, regTool, knowTool);

    const intentRouter = new IntentRouterService(mockAiGovernance);
    const promptBuilder = new ConversationalPromptBuilder();

    orchestrator = new ConversationalAssistantOrchestrator(
      repo,
      intentRouter,
      toolRegistry,
      promptBuilder,
      mockAiGovernance,
      mockComplianceClient,
      mockSummaryService,
      accessPolicy,
    );
  });

  it('should create and retrieve multi-turn conversation within tenant isolation', async () => {
    const conv = await orchestrator.createConversation(staffUser, 'vi');
    expect(conv.id).toBeDefined();
    expect(conv.tenantId).toBe('tenant-1');
    expect(conv.userId).toBe('user-1');

    const res = await orchestrator.getConversation(conv.id, staffUser);
    expect(res.conversation.id).toBe(conv.id);
    expect(res.messages).toEqual([]);

    // Cross-tenant access must fail
    const otherUser: CurrentUser = {
      tenantId: 'tenant-2',
      userId: 'user-2',
      actorType: ActorType.CUSTOMER,
      roles: ['CUSTOMER'],
      permissions: [],
    };
    await expect(orchestrator.getConversation(conv.id, otherUser)).rejects.toThrow();
  });

  it('should process greeting with deterministic sequence numbering and capability assistant.general', async () => {
    const conv = await orchestrator.createConversation(staffUser, 'vi');
    const result = await orchestrator.processMessage(conv.id, 'Xin chào!', staffUser);

    expect(result.role).toBe('ASSISTANT');
    expect(result.decision).toBe('NO_RAG');
    expect(result.sequenceNumber).toBe(2); // User: seq 1, Assistant: seq 2
    expect(result.governance?.capabilityCode).toBe('assistant.general');
    expect(mockAiGovernance.generate).toHaveBeenCalledWith(
      'assistant.general',
      expect.any(String),
      staffUser,
    );

    const { messages } = await orchestrator.getConversation(conv.id, staffUser);
    expect(messages.length).toBe(2);
    expect(messages[0].sequenceNumber).toBe(1);
    expect(messages[1].sequenceNumber).toBe(2);
  });

  it('should enforce customer tool authorization on shipments', async () => {
    const conv = await orchestrator.createConversation(customerUser, 'vi');
    const result = await orchestrator.processMessage(conv.id, 'Lô hàng của tôi ở đâu?', customerUser);

    expect(result.decision).toBe('DOMAIN_QUERY');
    expect(result.intent).toBe('SHIPMENT_QUERY');
    expect(mockAiGovernance.generate).toHaveBeenCalled();
  });

  it('should execute Stage 3 HYBRID synthesis with assistant.answer and validation RPC', async () => {
    const conv = await orchestrator.createConversation(staffUser, 'vi');
    const result = await orchestrator.processMessage(
      conv.id,
      'Luật yêu cầu gì và SOP nội bộ xử lý ra sao đối với pin lithium?',
      staffUser,
    );

    expect(result.decision).toBe('RAG_HYBRID');
    expect(result.governance?.capabilityCode).toBe('assistant.answer');
    expect(mockComplianceClient.queryRegulations).toHaveBeenCalled();
    expect(mockComplianceClient.queryKnowledge).toHaveBeenCalled();
    expect(mockAiGovernance.generate).toHaveBeenCalledWith(
      'assistant.answer',
      expect.any(String),
      staffUser,
    );
    expect(mockComplianceClient.validateGroundedEvidence).toHaveBeenCalled();
    expect(result.sources.regulatory.length).toBe(1);
    expect(result.sources.knowledge.length).toBe(1);
  });
});
