import { IntentRouterService } from './intent-router.service';
import { AssistantIntent } from '../../domain/enums/assistant-intent.enum';
import { NeedRagDecision } from '../../domain/enums/need-rag-decision.enum';
import { CurrentUser } from '../../infrastructure/security/current-user.interface';
import { ActorType } from '../../domain/enums/actor-type.enum';

describe('IntentRouterService', () => {
  let router: IntentRouterService;
  const mockAiGovernance: any = {
    generate: jest.fn(),
  };

  const user: CurrentUser = {
    tenantId: 'tenant-1',
    userId: 'user-1',
    actorType: ActorType.STAFF,
    roles: ['STAFF'],
    permissions: [],
  };

  beforeEach(() => {
    router = new IntentRouterService(mockAiGovernance);
    jest.clearAllMocks();
  });

  it('should route greetings to GENERAL with NO_RAG decision', async () => {
    const res = await router.classify('Xin chào bạn!', user);
    expect(res.intent).toBe(AssistantIntent.GENERAL);
    expect(res.decision).toBe(NeedRagDecision.NO_RAG);
    expect(mockAiGovernance.generate).not.toHaveBeenCalled();
  });

  it('should route shipment queries to SHIPMENT_QUERY with DOMAIN_QUERY decision', async () => {
    const res = await router.classify('Vận đơn SHP-2026-001 đang ở đâu?', user);
    expect(res.intent).toBe(AssistantIntent.SHIPMENT_QUERY);
    expect(res.decision).toBe(NeedRagDecision.DOMAIN_QUERY);
    expect(res.extractedParameters?.shipmentId).toBe('SHP-2026-001');
    expect(mockAiGovernance.generate).not.toHaveBeenCalled();
  });

  it('should route billing queries to BILLING_QUERY with DOMAIN_QUERY decision', async () => {
    const res = await router.classify('Kiểm tra công nợ và hóa đơn của tôi', user);
    expect(res.intent).toBe(AssistantIntent.BILLING_QUERY);
    expect(res.decision).toBe(NeedRagDecision.DOMAIN_QUERY);
    expect(mockAiGovernance.generate).not.toHaveBeenCalled();
  });

  it('should route customs laws to REGULATORY_QUERY with RAG_REGULATORY decision', async () => {
    const res = await router.classify('Quy định hải quan về xuất khẩu pin lithium', user);
    expect(res.intent).toBe(AssistantIntent.REGULATORY_QUERY);
    expect(res.decision).toBe(NeedRagDecision.RAG_REGULATORY);
  });

  it('should route internal guidelines to KNOWLEDGE_QUERY with RAG_KNOWLEDGE decision', async () => {
    const res = await router.classify('SOP quy trình nội bộ của công ty chúng tôi là gì?', user);
    expect(res.intent).toBe(AssistantIntent.KNOWLEDGE_QUERY);
    expect(res.decision).toBe(NeedRagDecision.RAG_KNOWLEDGE);
  });

  it('should route questions mentioning both law and SOP to HYBRID_RAG_QUERY with RAG_HYBRID decision', async () => {
    const res = await router.classify('Luật hải quan yêu cầu gì và SOP nội bộ xử lý ra sao?', user);
    expect(res.intent).toBe(AssistantIntent.HYBRID_RAG_QUERY);
    expect(res.decision).toBe(NeedRagDecision.RAG_HYBRID);
  });
});
