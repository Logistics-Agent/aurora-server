import { Test, TestingModule } from '@nestjs/testing';
import { ConfigService } from '@nestjs/config';
import {
  AiGovernanceNegotiationClient,
  GenerateSpeechInput,
} from './ai-governance-negotiation.client';

describe('AiGovernanceNegotiationClient', () => {
  let client: AiGovernanceNegotiationClient;
  let mockGrpcService: { Generate: jest.Mock };

  beforeEach(async () => {
    mockGrpcService = {
      Generate: jest.fn(),
    };

    const module: TestingModule = await Test.createTestingModule({
      providers: [
        AiGovernanceNegotiationClient,
        {
          provide: ConfigService,
          useValue: {
            get: jest.fn().mockReturnValue('localhost:9090'),
          },
        },
      ],
    }).compile();

    client = module.get<AiGovernanceNegotiationClient>(AiGovernanceNegotiationClient);
    (client as any).client = mockGrpcService;
  });

  const baseInput: GenerateSpeechInput = {
    decision: 'COUNTER_OFFER',
    offerPrice: 4000,
    counterOfferPrice: 4500,
    shipmentId: 'SHP-12345',
    round: 1,
    bottomPrice: 4200,
    listPrice: 5000,
    businessReason: 'Offer below bottom price',
    context: {
      tenantId: 'tenant-001',
      userId: 'user-001',
      traceId: 'trace-123',
    },
  };

  it('1. Successful structured LLM generation passes guards and returns AI speech', async () => {
    const validJsonOutput = JSON.stringify({
      action: 'COUNTER_OFFER',
      currency: 'USD',
      amount: 4500,
      message: 'Cảm ơn quý khách. Chúng tôi xin đề xuất mức giá tốt nhất là $4500.',
    });

    mockGrpcService.Generate.mockImplementation((req, meta, opts, callback) => {
      callback(null, {
        content: validJsonOutput,
        decision_id: 'dec-12345',
      });
    });

    const result = await client.generateNegotiationSpeech(baseInput);

    expect(result.aiDraftUsed).toBe(true);
    expect(result.fallbackUsed).toBe(false);
    expect(result.action).toBe('COUNTER_OFFER');
    expect(result.amount).toBe(4500);
    expect(result.currency).toBe('USD');
    expect(result.speech).toBe('Cảm ơn quý khách. Chúng tôi xin đề xuất mức giá tốt nhất là $4500.');
    expect(result.decisionId).toBe('dec-12345');
  });

  it('2. Action mismatch from LLM is rejected and falls back to deterministic template', async () => {
    // LLM maliciously or hallucinatorily returns ACCEPT when domain decision is COUNTER_OFFER
    const invalidActionJson = JSON.stringify({
      action: 'ACCEPT',
      currency: 'USD',
      amount: 4000,
      message: 'Chúng tôi chấp nhận giá của bạn $4000!',
    });

    mockGrpcService.Generate.mockImplementation((req, meta, opts, callback) => {
      callback(null, {
        content: invalidActionJson,
        decision_id: 'dec-hallucination',
      });
    });

    const result = await client.generateNegotiationSpeech(baseInput);

    expect(result.aiDraftUsed).toBe(false);
    expect(result.fallbackUsed).toBe(true);
    expect(result.action).toBe('COUNTER_OFFER');
    expect(result.amount).toBe(4500);
    expect(result.speech).toContain('$4500');
    expect(result.speech).toContain('Cảm ơn đề xuất $4000');
  });

  it('3. Amount mismatch from LLM is rejected and falls back to deterministic template', async () => {
    // LLM outputs incorrect counter-offer amount ($4200 instead of approved $4500)
    const invalidAmountJson = JSON.stringify({
      action: 'COUNTER_OFFER',
      currency: 'USD',
      amount: 4200,
      message: 'Mức giá đề xuất là $4200.',
    });

    mockGrpcService.Generate.mockImplementation((req, meta, opts, callback) => {
      callback(null, {
        content: invalidAmountJson,
        decision_id: 'dec-wrong-amount',
      });
    });

    const result = await client.generateNegotiationSpeech(baseInput);

    expect(result.aiDraftUsed).toBe(false);
    expect(result.fallbackUsed).toBe(true);
    expect(result.amount).toBe(4500);
    expect(result.speech).toContain('$4500');
  });

  it('4. gRPC error/timeout safely returns deterministic fallback speech', async () => {
    mockGrpcService.Generate.mockImplementation((req, meta, opts, callback) => {
      callback(new Error('AiGovernance connection timeout'), null);
    });

    const result = await client.generateNegotiationSpeech(baseInput);

    expect(result.aiDraftUsed).toBe(false);
    expect(result.fallbackUsed).toBe(true);
    expect(result.action).toBe('COUNTER_OFFER');
    expect(result.amount).toBe(4500);
    expect(result.speech).toBe(client.getFallbackSpeech(baseInput));
  });

  it('5. Non-JSON or malformed output triggers safe fallback', async () => {
    mockGrpcService.Generate.mockImplementation((req, meta, opts, callback) => {
      callback(null, {
        content: 'This is plain text without valid JSON wrapper.',
        decision_id: 'dec-plain-text',
      });
    });

    const result = await client.generateNegotiationSpeech(baseInput);

    expect(result.aiDraftUsed).toBe(false);
    expect(result.fallbackUsed).toBe(true);
    expect(result.speech).toBe(client.getFallbackSpeech(baseInput));
  });
});
