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
    lastSuggestedAmount: 4520,
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
    },
    negotiationMessage: {
      create: jest.fn().mockResolvedValue({
        id: 'msg-001',
        createdAt: new Date('2026-08-25T12:00:00Z'),
      }),
    },
    $transaction: jest.fn().mockImplementation((callback) => callback(mockPrisma)),
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
      sourceMessageId: 'offer-counter-1',
    });

    expect(result.decision).toBe('COUNTER_OFFER');
    expect(result.counterOfferPrice).toBeGreaterThan(4200);
    expect(result.currency).toBe('USD');
    expect(result.suggestedReplyAvailable).toBe(true);
    expect(result.aiDraftUsed).toBe(true);
    expect(result.fallbackUsed).toBe(false);
    expect(result.suggestedReply.subjectSuggestion).toContain('SHP-001');
    expect(mockPrisma.negotiationMessage.create).toHaveBeenCalledWith({
      data: expect.objectContaining({
        customerOfferPrice: 4000,
        sourceMessageId: 'offer-counter-1',
        offerPrice: result.counterOfferPrice,
      }),
    });
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
      sourceMessageId: 'offer-vip-1',
      customerTier: 'VIP',
    });

    expect(result.decision).toBe('HUMAN_HANDOFF');
    expect(result.status).toBe('HANDOFF');
    expect(result.approvedAmount).toBe(0);
    expect(mockAiClient.generateNegotiationDraft).not.toHaveBeenCalled();
  });

  it('3. Offer above floor proposes staff approval without marking a final agreement', async () => {
    mockAiClient.generateNegotiationDraft.mockResolvedValueOnce({
      content: 'We can proceed at $4,300 USD, subject to staff review and your confirmation.',
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
      sourceMessageId: 'offer-accept-1',
    });

    expect(result.decision).toBe('ACCEPT');
    expect(result.status).toBe('PENDING_APPROVAL');
    expect(result.suggestedAmount).toBe(4300);
    expect(result.aiSpeech).not.toContain('has been accepted');
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
      sourceMessageId: 'offer-prod-1',
    })).rejects.toThrow('Authoritative shipment pricing is not connected');
    expect(mockPrisma.negotiationSession.create).not.toHaveBeenCalled();
  });

  it('does not process a production session without approved pricing evidence', async () => {
    mockConfig.get.mockReturnValue('production');
    await expect(service.submitOffer({
      tenantId: 'tenant-001', shipmentId: 'SHP-001', customerId: 'cust-001',
      offerPrice: 4300, sourceMessageId: 'offer-unapproved-1',
    })).rejects.toThrow('no approved pricing evidence');
    expect(mockAiClient.generateNegotiationDraft).not.toHaveBeenCalled();
  });

  it('rejects a stale round without recording an AI message', async () => {
    mockPrisma.negotiationSession.updateMany.mockResolvedValueOnce({ count: 0 });
    await expect(service.submitOffer({
      tenantId: 'tenant-001', shipmentId: 'SHP-001', customerId: 'cust-001',
      offerPrice: 4000, sourceMessageId: 'offer-stale-1',
    })).rejects.toThrow('Negotiation changed');
    expect(mockPrisma.negotiationMessage.create).not.toHaveBeenCalled();
  });

  it('rejects a duplicate customer offer reference', async () => {
    mockPrisma.negotiationMessage.create.mockRejectedValueOnce({ code: 'P2002' });
    await expect(service.submitOffer({
      tenantId: 'tenant-001', shipmentId: 'SHP-001', customerId: 'cust-001',
      offerPrice: 4000, sourceMessageId: 'offer-replayed-1',
    })).rejects.toThrow('already been processed');
  });

  it('does not open a second negotiation while staff approval is pending', async () => {
    mockPrisma.negotiationSession.findFirst.mockResolvedValueOnce({
      ...mockSession,
      status: 'PENDING_APPROVAL',
    });
    await expect(service.submitOffer({
      tenantId: 'tenant-001', shipmentId: 'SHP-001', customerId: 'cust-001',
      offerPrice: 4300, sourceMessageId: 'offer-after-accept-1',
    })).rejects.toThrow('awaiting staff action');
    expect(mockPrisma.negotiationSession.create).not.toHaveBeenCalled();
    expect(mockPrisma.negotiationSession.findFirst).toHaveBeenCalledWith({
      where: {
        tenantId: 'tenant-001',
        shipmentId: 'SHP-001',
        customerId: 'cust-001',
        status: { in: ['OPEN', 'PENDING_APPROVAL'] },
      },
    });
  });

  it('uses deterministic wording when AI claims a final deal or changes currency', async () => {
    mockAiClient.generateNegotiationDraft.mockResolvedValueOnce({
      content: 'We accept the deal at $4,400 EUR. It is confirmed.',
      isFallback: false,
    });
    const result = await service.submitOffer({
      tenantId: 'tenant-001', shipmentId: 'SHP-001', customerId: 'cust-001',
      offerPrice: 4000, sourceMessageId: 'offer-wrong-wording-1',
    });

    expect(result.fallbackUsed).toBe(true);
    expect(result.aiDraftUsed).toBe(false);
    expect(result.aiSpeech).toBe('Deterministic fallback wording.');
  });

  it('rejects fractional cents before generating a draft', async () => {
    await expect(service.submitOffer({
      tenantId: 'tenant-001', shipmentId: 'SHP-001', customerId: 'cust-001',
      offerPrice: 4000.001, sourceMessageId: 'offer-fraction-1',
    })).rejects.toThrow('two decimal places');
    expect(mockAiClient.generateNegotiationDraft).not.toHaveBeenCalled();
  });
});
