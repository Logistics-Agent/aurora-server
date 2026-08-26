import { Test, TestingModule } from '@nestjs/testing';
import { NegotiationService } from './negotiation.service';
import { PrismaService } from '../../infrastructure/prisma/prisma.service';
import { NegotiationStrategyDomainService } from '../../domain/services/negotiation-strategy.domain-service';
import { AiGovernanceNegotiationClient } from '../../infrastructure/grpc/ai-governance.grpc-client';

describe('NegotiationService', () => {
  let service: NegotiationService;
  let prisma: PrismaService;
  let strategy: NegotiationStrategyDomainService;
  let aiClient: AiGovernanceNegotiationClient;

  const mockSession = {
    id: 'sess-001',
    tenantId: 'tenant-001',
    shipmentId: 'SHP-001',
    customerId: 'cust-001',
    status: 'OPEN',
    currentRound: 1,
    maxRounds: 5,
    listPrice: 5000,
    bottomPrice: 4200,
    currency: 'USD',
    suggestedSubject: 'Re: Quotation Proposal for Shipment SHP-001',
    suggestedBody: 'Thank you for your proposal.',
    suggestedLanguage: 'en',
    suggestedReplyAvailable: true,
    aiDraftUsed: true,
    fallbackUsed: false,
    lastDecision: 'COUNTER_OFFER',
    lastApprovedAmount: 4520,
    sourceMessageId: 'msg-inbound-123',
    sourceThreadId: 'thread-456',
  };

  const mockPrisma = {
    negotiationSession: {
      findFirst: jest.fn().mockResolvedValue(mockSession),
      create: jest.fn().mockResolvedValue(mockSession),
      update: jest.fn().mockResolvedValue({ ...mockSession, currentRound: 2 }),
      findUnique: jest.fn().mockResolvedValue(mockSession),
    },
    negotiationMessage: {
      create: jest.fn().mockResolvedValue({
        id: 'msg-001',
        createdAt: new Date('2026-08-25T12:00:00Z'),
      }),
    },
    $transaction: jest.fn().mockImplementation((promises) => Promise.all(promises)),
  };

  const mockAiClient = {
    generateNegotiationDraft: jest.fn().mockResolvedValue({
      content: 'Dear Customer, our best counter-offer is $4,400.00 USD.',
      decisionId: 'dec-123',
      automationLevel: 'ASSISTED',
      requiresApproval: false,
      inputTokens: 20,
      outputTokens: 40,
      isFallback: false,
    }),
    getDeterministicFallback: jest.fn().mockReturnValue('Deterministic fallback wording.'),
  };

  beforeEach(async () => {
    jest.clearAllMocks();
    mockAiClient.generateNegotiationDraft.mockResolvedValue({
      content: 'Dear Customer, our best counter-offer is $4,400.00 USD.',
      decisionId: 'dec-123',
      automationLevel: 'ASSISTED',
      requiresApproval: false,
      inputTokens: 20,
      outputTokens: 40,
      isFallback: false,
    });

    const module: TestingModule = await Test.createTestingModule({
      providers: [
        NegotiationService,
        NegotiationStrategyDomainService,
        {
          provide: PrismaService,
          useValue: mockPrisma,
        },
        {
          provide: AiGovernanceNegotiationClient,
          useValue: mockAiClient,
        },
      ],
    }).compile();

    service = module.get<NegotiationService>(NegotiationService);
    prisma = module.get<PrismaService>(PrismaService);
    strategy = module.get<NegotiationStrategyDomainService>(NegotiationStrategyDomainService);
    aiClient = module.get<AiGovernanceNegotiationClient>(AiGovernanceNegotiationClient);
  });

  it('1. Evaluates COUNTER_OFFER and delegates wording to AiGovernance (USD Currency MVP)', async () => {
    const result = await service.submitOffer({
      tenantId: 'tenant-001',
      shipmentId: 'SHP-001',
      customerId: 'cust-001',
      offerPrice: 4000,
    });

    expect(result.decision).toBe('COUNTER_OFFER');
    expect(result.counterOfferPrice).toBeGreaterThan(4200);
    expect(result.currency).toBe('USD');
    expect(result.suggestedReplyAvailable).toBe(true);
    expect(result.aiDraftUsed).toBe(true);
    expect(result.fallbackUsed).toBe(false);
    expect(result.suggestedReply.subjectSuggestion).toContain('SHP-001');
    expect(mockAiClient.generateNegotiationDraft).toHaveBeenCalledWith(
      expect.objectContaining({
        action: 'COUNTER_OFFER',
        customerOffer: 4000,
        shipmentId: 'SHP-001',
        currency: 'USD',
      }),
    );
  });

  it('2. VIP customer triggers HUMAN_HANDOFF with account manager handoff', async () => {
    mockAiClient.generateNegotiationDraft.mockResolvedValueOnce({
      content: 'Dear Customer, your request has been escalated to a personal account manager.',
      decisionId: 'dec-124',
      automationLevel: 'ASSISTED',
      requiresApproval: false,
      inputTokens: 20,
      outputTokens: 30,
      isFallback: false,
    });

    const result = await service.submitOffer({
      tenantId: 'tenant-001',
      shipmentId: 'SHP-001',
      customerId: 'cust-001',
      offerPrice: 4000,
      customerTier: 'VIP',
    });

    expect(result.decision).toBe('HUMAN_HANDOFF');
    expect(result.status).toBe('HANDOFF');
  });

  it('3. Offer above bottom price triggers ACCEPT with deal confirmation', async () => {
    mockAiClient.generateNegotiationDraft.mockResolvedValueOnce({
      content: 'Dear Customer, we are pleased to confirm that your offer has been accepted.',
      decisionId: 'dec-125',
      automationLevel: 'ASSISTED',
      requiresApproval: false,
      inputTokens: 20,
      outputTokens: 30,
      isFallback: false,
    });

    const result = await service.submitOffer({
      tenantId: 'tenant-001',
      shipmentId: 'SHP-001',
      customerId: 'cust-001',
      offerPrice: 4300, // Above bottomPrice 4200
    });

    expect(result.decision).toBe('ACCEPT');
    expect(result.status).toBe('ACCEPTED');
  });

  it('4. GetDraftSuggestion reads persisted session without calling AI again', async () => {
    const suggestion = await service.getDraftSuggestion('sess-001');

    expect(suggestion.negotiationSessionId).toBe('sess-001');
    expect(suggestion.decision).toBe('COUNTER_OFFER');
    expect(suggestion.suggestedReplyAvailable).toBe(true);
    expect(suggestion.sourceMessageId).toBe('msg-inbound-123');
    expect(suggestion.sourceThreadId).toBe('thread-456');
    expect(mockAiClient.generateNegotiationDraft).not.toHaveBeenCalled();
  });
});
