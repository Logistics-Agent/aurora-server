import { Test, TestingModule } from '@nestjs/testing';
import { NegotiationService } from './negotiation.service';
import { PrismaService } from '../../infrastructure/prisma/prisma.service';
import { NegotiationStrategyDomainService } from '../../domain/services/negotiation-strategy.domain-service';
import { AiGovernanceNegotiationClient } from '../../infrastructure/ai/ai-governance-negotiation.client';

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
  };

  const mockPrisma = {
    negotiationSession: {
      findFirst: jest.fn().mockResolvedValue(mockSession),
      create: jest.fn().mockResolvedValue(mockSession),
      update: jest.fn().mockResolvedValue({ ...mockSession, currentRound: 2 }),
      findUnique: jest.fn(),
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
    generateNegotiationSpeech: jest.fn().mockResolvedValue({
      speech: 'Cảm ơn quý khách. Chúng tôi xin đề xuất mức giá $4500.',
      action: 'COUNTER_OFFER',
      amount: 4500,
      currency: 'USD',
      aiDraftUsed: true,
      fallbackUsed: false,
      decisionId: 'dec-123',
    }),
  };

  beforeEach(async () => {
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

  it('1. Evaluates COUNTER_OFFER and delegates natural speech drafting to AiGovernance', async () => {
    const result = await service.submitOffer({
      tenantId: 'tenant-001',
      shipmentId: 'SHP-001',
      customerId: 'cust-001',
      offerPrice: 4000,
    });

    expect(result.decision).toBe('COUNTER_OFFER');
    expect(result.counterOfferPrice).toBeGreaterThan(4200);
    expect(result.aiDraftUsed).toBe(true);
    expect(result.fallbackUsed).toBe(false);
    expect(result.decisionId).toBe('dec-123');
    expect(mockAiClient.generateNegotiationSpeech).toHaveBeenCalledWith(
      expect.objectContaining({
        decision: 'COUNTER_OFFER',
        offerPrice: 4000,
        shipmentId: 'SHP-001',
      }),
    );
  });

  it('2. VIP customer triggers HUMAN_HANDOFF with account manager handoff speech', async () => {
    mockAiClient.generateNegotiationSpeech.mockResolvedValueOnce({
      speech: 'Đề xuất cần được xem xét bởi chuyên viên cao cấp.',
      action: 'HUMAN_HANDOFF',
      currency: 'USD',
      aiDraftUsed: true,
      fallbackUsed: false,
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
    mockAiClient.generateNegotiationSpeech.mockResolvedValueOnce({
      speech: 'Chúng tôi xin chấp nhận mức giá $4300.',
      action: 'ACCEPT',
      amount: 4300,
      currency: 'USD',
      aiDraftUsed: true,
      fallbackUsed: false,
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
});
