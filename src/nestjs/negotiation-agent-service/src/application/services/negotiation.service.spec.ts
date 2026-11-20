import { Test, TestingModule } from '@nestjs/testing';
import { NegotiationService } from './negotiation.service';
import { PrismaService } from '../../infrastructure/prisma/prisma.service';
import { NegotiationStrategyDomainService } from '../../domain/services/negotiation-strategy.domain-service';
import { AiGovernanceNegotiationClient } from '../../infrastructure/grpc/ai-governance.grpc-client';
import { ConfigService } from '@nestjs/config';

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
      updateMany: jest.fn().mockResolvedValue({ count: 1 }),
      findUnique: jest.fn().mockResolvedValue(mockSession),
      findMany: jest.fn().mockResolvedValue([mockSession]),
      count: jest.fn().mockResolvedValue(1),
    },
    negotiationMessage: {
      create: jest.fn().mockResolvedValue({
        id: 'msg-001',
        createdAt: new Date('2026-08-25T12:00:00Z'),
      }),
    },
    $transaction: jest.fn().mockImplementation((arg) =>
      typeof arg === 'function' ? arg(mockPrisma) : Promise.all(arg),
    ),
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
  const mockConfig = { get: jest.fn().mockReturnValue('test') };

  beforeEach(async () => {
    jest.clearAllMocks();
    mockConfig.get.mockReturnValue('test');
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
        { provide: ConfigService, useValue: mockConfig },
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
    const suggestion = await service.getDraftSuggestion('sess-001', 'tenant-001');

    expect(suggestion.negotiationSessionId).toBe('sess-001');
    expect(suggestion.decision).toBe('COUNTER_OFFER');
    expect(suggestion.suggestedReplyAvailable).toBe(true);
    expect(suggestion.sourceMessageId).toBe('msg-inbound-123');
    expect(suggestion.sourceThreadId).toBe('thread-456');
    expect(mockAiClient.generateNegotiationDraft).not.toHaveBeenCalled();
  });

  it('does not expose a negotiation session outside its tenant', async () => {
    mockPrisma.negotiationSession.findFirst.mockResolvedValueOnce(null);
    await expect(service.getDraftSuggestion('sess-001', 'tenant-other')).rejects.toThrow();
    expect(mockPrisma.negotiationSession.findFirst).toHaveBeenCalledWith({
      where: { id: 'sess-001', tenantId: 'tenant-other' },
    });
  });

  it('does not open a production session from caller-supplied prices', async () => {
    mockConfig.get.mockReturnValue('production');
    mockPrisma.negotiationSession.findFirst.mockResolvedValueOnce(null);
    await expect(service.submitOffer({
      tenantId: 'tenant-001', shipmentId: 'SHP-001', customerId: 'cust-001',
      offerPrice: 4000, listPrice: 5000, bottomPrice: 4200,
    })).rejects.toThrow('Authoritative shipment pricing is not connected');
    expect(mockPrisma.negotiationSession.create).not.toHaveBeenCalled();
  });

  it('accepts AI draft that echoes customer offer ($4,000) alongside approved counter-offer ($4,400)', async () => {
    mockAiClient.generateNegotiationDraft.mockResolvedValueOnce({
      content: 'Thank you for your offer of $4,000.00 USD. Our best counter-offer is $4,400.00 USD.',
      decisionId: 'dec-126',
      automationLevel: 'ASSISTED',
      requiresApproval: false,
      inputTokens: 20,
      outputTokens: 40,
      isFallback: false,
    });

    const result = await service.submitOffer({
      tenantId: 'tenant-001',
      shipmentId: 'SHP-001',
      customerId: 'cust-001',
      offerPrice: 4000,
    });

    expect(result.aiDraftUsed).toBe(true);
    expect(result.fallbackUsed).toBe(false);
    expect(mockPrisma.negotiationMessage.create).toHaveBeenCalledTimes(2);
  });

  it('anchors high against lowball offers in Round 1 and concedes monotonically when customer raises offer in Round 2', () => {
    // Lowball $1,000 in Round 1 -> anchors at $4,880 (never exposes $4,200 bottomPrice)
    const lowballR1 = strategy.determineDecision({
      offerPrice: 1000,
      bottomPrice: 4200,
      listPrice: 5000,
      currentRound: 1,
      maxRounds: 5,
    });
    expect(lowballR1.counterOfferPrice).toBe(4880);

    // Good-faith Round 1 ($4,000) -> $4,400; Round 2 ($4,100) -> concedes below $4,400 ($4,362.50)
    const goodR2 = strategy.determineDecision({
      offerPrice: 4100,
      bottomPrice: 4200,
      listPrice: 5000,
      currentRound: 2,
      maxRounds: 5,
      previousApprovedAmount: 4400,
    });
    expect(goodR2.counterOfferPrice).toBeLessThan(4400);
    expect(goodR2.counterOfferPrice).toBeGreaterThanOrEqual(4200);
  });

  it('rejects concurrent offer submission with ConflictException via OCC guard', async () => {
    mockPrisma.negotiationSession.updateMany.mockResolvedValueOnce({ count: 0 });
    await expect(
      service.submitOffer({
        tenantId: 'tenant-001',
        shipmentId: 'SHP-001',
        customerId: 'cust-001',
        offerPrice: 4000,
      }),
    ).rejects.toThrow('modified concurrently or is no longer open');
    expect(mockPrisma.negotiationMessage.create).not.toHaveBeenCalled();
  });

  it('requires shipmentId strictly and blocks AI wording that leaks internal cost or bottom floor price', async () => {
    await expect(
      service.submitOffer({
        tenantId: 'tenant-001',
        shipmentId: '',
        customerId: 'cust-001',
        offerPrice: 4000,
      }),
    ).rejects.toThrow('shipmentId, customerId and a positive offerPrice are required');

    mockAiClient.generateNegotiationDraft.mockResolvedValueOnce({
      content: 'Dear Customer, our giá sàn is $4,200.00 USD so best counter is $4,400.00 USD.',
      decisionId: 'dec-leak-1',
      automationLevel: 'ASSISTED',
      requiresApproval: false,
      inputTokens: 20,
      outputTokens: 30,
      isFallback: false,
    });

    const leakBlocked = await service.submitOffer({
      tenantId: 'tenant-001',
      shipmentId: 'SHP-001',
      quoteId: 'quote-1',
      sourceThreadId: 'thread-abc',
      customerId: 'cust-001',
      offerPrice: 4000,
    });

    expect(leakBlocked.aiDraftUsed).toBe(false);
    expect(leakBlocked.fallbackUsed).toBe(true);
    expect(leakBlocked.aiSpeech).not.toContain('giá sàn');
    expect(leakBlocked.aiSpeech).not.toContain('4,200');

    mockPrisma.negotiationSession.findMany.mockResolvedValueOnce([
      {
        ...mockSession,
        quoteId: 'quote-1',
        sourceThreadId: 'thread-abc',
        createdAt: new Date('2026-11-20T00:00:00Z'),
        updatedAt: new Date('2026-11-20T00:00:00Z'),
      },
    ]);
    mockPrisma.negotiationSession.count.mockResolvedValueOnce(1);
    const listed = await service.listSessions({
      tenantId: 'tenant-001',
      sourceThreadId: 'thread-abc',
      shipmentId: 'SHP-001',
      quoteId: 'quote-1',
    });
    expect(listed.total).toBe(1);
    expect(listed.sessions[0].shipmentId).toBe('SHP-001');
  });
});
