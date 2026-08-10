import { Injectable, Logger, NotFoundException } from '@nestjs/common';
import { PrismaService } from '../../infrastructure/prisma/prisma.service';
import { NegotiationStrategyDomainService } from '../../domain/services/negotiation-strategy.domain-service';
import { GeminiAIClient } from '../../infrastructure/ai/gemini.client';

export interface SubmitOfferInput {
  tenantId: string;
  shipmentId: string;
  customerId: string;
  offerPrice: number;
  listPrice?: number;
  bottomPrice?: number;
  customerTier?: string;
}

export interface NegotiationResult {
  sessionId: string;
  shipmentId: string;
  round: number;
  decision: string;
  counterOfferPrice?: number;
  aiSpeech: string;
  status: string;
  createdAt: string;
}

@Injectable()
export class NegotiationService {
  private readonly logger = new Logger(NegotiationService.name);

  constructor(
    private readonly prisma: PrismaService,
    private readonly strategy: NegotiationStrategyDomainService,
    private readonly geminiClient: GeminiAIClient,
  ) {}

  async submitOffer(input: SubmitOfferInput): Promise<NegotiationResult> {
    const tenantId = input.tenantId || 'a0000000-0000-0000-0000-000000000001';
    const listPrice = input.listPrice || 1500.0;
    const bottomPrice = input.bottomPrice || 1200.0;

    // 1. Get or create active negotiation session
    let session = await this.prisma.negotiationSession.findFirst({
      where: {
        tenantId,
        shipmentId: input.shipmentId,
        customerId: input.customerId,
        status: 'OPEN',
      },
    });

    if (!session) {
      session = await this.prisma.negotiationSession.create({
        data: {
          tenantId,
          shipmentId: input.shipmentId,
          customerId: input.customerId,
          status: 'OPEN',
          currentRound: 1,
          maxRounds: 5,
          listPrice,
          bottomPrice,
        },
      });
    }

    // 2. Evaluate Strategy Decision via Deterministic Engine
    const strategyResult = this.strategy.determineDecision({
      offerPrice: input.offerPrice,
      bottomPrice: session.bottomPrice,
      listPrice: session.listPrice,
      currentRound: session.currentRound,
      maxRounds: session.maxRounds,
      customerTier: input.customerTier,
    });

    // 3. Generate Speech via Gemini AI
    const speech = await this.geminiClient.generateNegotiationSpeech({
      decision: strategyResult.decision,
      offerPrice: input.offerPrice,
      counterOfferPrice: strategyResult.counterOfferPrice,
      shipmentId: input.shipmentId,
      round: session.currentRound,
    });

    // 4. Determine new session status
    let newStatus = session.status;
    if (strategyResult.decision === 'ACCEPT') {
      newStatus = 'ACCEPTED';
    } else if (strategyResult.decision === 'HUMAN_HANDOFF') {
      newStatus = 'HANDOFF';
    }

    // 5. ACID Transaction: Save message + update session round/status
    const [savedMsg, updatedSession] = await this.prisma.$transaction([
      this.prisma.negotiationMessage.create({
        data: {
          sessionId: session.id,
          round: session.currentRound,
          sender: 'AI',
          message: speech,
          offerPrice: strategyResult.counterOfferPrice || input.offerPrice,
          decision: strategyResult.decision,
        },
      }),
      this.prisma.negotiationSession.update({
        where: { id: session.id },
        data: {
          status: newStatus,
          currentRound: { increment: 1 },
        },
      }),
    ]);

    this.logger.log(
      `[Negotiation] Session ${session.id} | Round ${session.currentRound} | Decision: ${strategyResult.decision} | Speech: "${speech}"`,
    );

    return {
      sessionId: session.id,
      shipmentId: input.shipmentId,
      round: session.currentRound,
      decision: strategyResult.decision,
      counterOfferPrice: strategyResult.counterOfferPrice,
      aiSpeech: speech,
      status: newStatus,
      createdAt: savedMsg.createdAt.toISOString(),
    };
  }

  async getSessionHistory(sessionId: string) {
    const session = await this.prisma.negotiationSession.findUnique({
      where: { id: sessionId },
      include: { messages: { orderBy: { createdAt: 'asc' } } },
    });

    if (!session) {
      throw new NotFoundException(`Negotiation Session ${sessionId} not found`);
    }

    return session;
  }
}
