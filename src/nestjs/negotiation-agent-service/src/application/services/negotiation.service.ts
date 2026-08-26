import { Injectable, Logger, NotFoundException } from '@nestjs/common';
import { PrismaService } from '../../infrastructure/prisma/prisma.service';
import { NegotiationStrategyDomainService, DEFAULT_NEGOTIATION_CURRENCY } from '../../domain/services/negotiation-strategy.domain-service';
import { AiGovernanceNegotiationClient } from '../../infrastructure/grpc/ai-governance.grpc-client';

export interface SubmitOfferInput {
  tenantId?: string;
  userId?: string;
  traceId?: string;
  shipmentId?: string;
  customerId?: string;
  offerPrice?: number;
  listPrice?: number;
  bottomPrice?: number;
  customerTier?: string;
  sessionId?: string;
  sourceMessageId?: string;
  sourceThreadId?: string;
  // snake_case support for raw Protobuf gRPC payloads
  shipment_id?: string;
  customer_id?: string;
  offer_price?: number;
  list_price?: number;
  bottom_price?: number;
  customer_tier?: string;
  session_id?: string;
  source_message_id?: string;
  source_thread_id?: string;
}

export interface SuggestedReplyDto {
  subjectSuggestion: string;
  body: string;
  language: string;
}

export interface NegotiationResult {
  sessionId: string;
  shipmentId: string;
  round: number;
  decision: string;
  counterOfferPrice?: number;
  approvedAmount: number;
  currency: string;
  aiSpeech: string;
  status: string;
  createdAt: string;
  suggestedReply: SuggestedReplyDto;
  suggestedReplyAvailable: boolean;
  aiDraftUsed: boolean;
  fallbackUsed: boolean;
}

export interface DraftSuggestionResult {
  negotiationSessionId: string;
  shipmentId: string;
  suggestedReplyAvailable: boolean;
  aiDraftUsed: boolean;
  fallbackUsed: boolean;
  subject: string;
  body: string;
  language: string;
  decision: string;
  approvedAmount: number;
  currency: string;
  sourceMessageId: string;
  sourceThreadId: string;
}

@Injectable()
export class NegotiationService {
  private readonly logger = new Logger(NegotiationService.name);

  constructor(
    private readonly prisma: PrismaService,
    private readonly strategy: NegotiationStrategyDomainService,
    private readonly aiGovernanceClient: AiGovernanceNegotiationClient,
  ) {}

  async submitOffer(input: SubmitOfferInput): Promise<NegotiationResult> {
    const tenantId = input.tenantId || 'a0000000-0000-0000-0000-000000000001';
    const shipmentId = input.shipmentId || input.shipment_id || 'UNKNOWN-SHIPMENT';
    const customerId = input.customerId || input.customer_id || 'UNKNOWN-CUSTOMER';
    const offerPrice = input.offerPrice ?? input.offer_price ?? 0;
    const listPrice = input.listPrice ?? input.list_price ?? 1500.0;
    const bottomPrice = input.bottomPrice ?? input.bottom_price ?? 1200.0;
    const customerTier = input.customerTier || input.customer_tier;
    const requestedSessionId = input.sessionId || input.session_id;
    const sourceMessageId = input.sourceMessageId || input.source_message_id || null;
    const sourceThreadId = input.sourceThreadId || input.source_thread_id || null;
    const currency = DEFAULT_NEGOTIATION_CURRENCY;

    // 1. Get or create active negotiation session
    let session = requestedSessionId
      ? await this.prisma.negotiationSession.findUnique({ where: { id: requestedSessionId } })
      : await this.prisma.negotiationSession.findFirst({
          where: {
            tenantId,
            shipmentId,
            customerId,
            status: 'OPEN',
          },
        });

    if (!session) {
      session = await this.prisma.negotiationSession.create({
        data: {
          tenantId,
          shipmentId,
          customerId,
          status: 'OPEN',
          currentRound: 1,
          maxRounds: 5,
          listPrice,
          bottomPrice,
          currency,
          sourceMessageId,
          sourceThreadId,
        },
      });
    }

    // 2. Evaluate Strategy Decision via Deterministic Engine
    const strategyResult = this.strategy.determineDecision({
      offerPrice,
      bottomPrice: session.bottomPrice,
      listPrice: session.listPrice,
      currentRound: session.currentRound,
      maxRounds: session.maxRounds,
      customerTier,
      currency,
    });

    // 3. Generate Wording via AiGovernance under capability 'negotiation.draft'
    const aiResult = await this.aiGovernanceClient.generateNegotiationDraft({
      action: strategyResult.decision,
      approvedAmount: strategyResult.approvedAmount,
      currency: strategyResult.currency,
      customerOffer: offerPrice,
      shipmentId,
      round: session.currentRound,
      customerTier,
      tenantId,
      userId: input.userId,
      traceId: input.traceId,
    });

    // 4. Validate AI Output against Deterministic Decision & Pricing Guardrails
    let finalBody = aiResult.content;
    let aiDraftUsed = !aiResult.isFallback;
    let fallbackUsed = aiResult.isFallback;

    // Validation Guardrails: Check if AI output violates deterministic decision or price
    const isWordingValid = this.validateAiWording(
      aiResult.content,
      strategyResult.decision,
      strategyResult.approvedAmount,
      strategyResult.currency,
    );

    if (!isWordingValid && !aiResult.isFallback) {
      this.logger.warn(
        `[Negotiation] AI wording validation failed for session ${session.id}. Discarding AI text and using deterministic fallback.`,
      );
      finalBody = this.aiGovernanceClient.getDeterministicFallback({
        action: strategyResult.decision,
        approvedAmount: strategyResult.approvedAmount,
        currency: strategyResult.currency,
        customerOffer: offerPrice,
        shipmentId,
        round: session.currentRound,
      });
      aiDraftUsed = false;
      fallbackUsed = true;
    }

    const suggestedSubject = `Re: Quotation Proposal for Shipment ${shipmentId}`;
    const suggestedLanguage = 'en';

    // 5. Determine new session status
    let newStatus = session.status;
    if (strategyResult.decision === 'ACCEPT') {
      newStatus = 'ACCEPTED';
    } else if (strategyResult.decision === 'HUMAN_HANDOFF') {
      newStatus = 'HANDOFF';
    } else if (strategyResult.decision === 'REJECT') {
      newStatus = 'REJECTED';
    }

    // 6. ACID Transaction: Save message + update session round/status/suggestedReply
    const [savedMsg, updatedSession] = await this.prisma.$transaction([
      this.prisma.negotiationMessage.create({
        data: {
          sessionId: session.id,
          round: session.currentRound,
          sender: 'AI',
          message: finalBody,
          offerPrice: strategyResult.counterOfferPrice || offerPrice,
          decision: strategyResult.decision,
          currency: strategyResult.currency,
        },
      }),
      this.prisma.negotiationSession.update({
        where: { id: session.id },
        data: {
          status: newStatus,
          currentRound: { increment: 1 },
          suggestedSubject,
          suggestedBody: finalBody,
          suggestedLanguage,
          suggestedReplyAvailable: true,
          aiDraftUsed,
          fallbackUsed,
          lastDecision: strategyResult.decision,
          lastApprovedAmount: strategyResult.approvedAmount,
          sourceMessageId: sourceMessageId || session.sourceMessageId,
          sourceThreadId: sourceThreadId || session.sourceThreadId,
        },
      }),
    ]);

    this.logger.log(
      `[Negotiation] Session ${session.id} | Round ${session.currentRound} | Decision: ${strategyResult.decision} | SuggestedReply Available (AI Used: ${aiDraftUsed}, Fallback Used: ${fallbackUsed})`,
    );

    return {
      sessionId: session.id,
      shipmentId,
      round: session.currentRound,
      decision: strategyResult.decision,
      counterOfferPrice: strategyResult.counterOfferPrice,
      approvedAmount: strategyResult.approvedAmount,
      currency: strategyResult.currency,
      aiSpeech: finalBody,
      status: newStatus,
      createdAt: savedMsg.createdAt.toISOString(),
      suggestedReply: {
        subjectSuggestion: suggestedSubject,
        body: finalBody,
        language: suggestedLanguage,
      },
      suggestedReplyAvailable: true,
      aiDraftUsed,
      fallbackUsed,
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

  async getDraftSuggestion(sessionId: string): Promise<DraftSuggestionResult> {
    const session = await this.prisma.negotiationSession.findUnique({
      where: { id: sessionId },
    });

    if (!session) {
      throw new NotFoundException(`Negotiation Session ${sessionId} not found`);
    }

    return {
      negotiationSessionId: session.id,
      shipmentId: session.shipmentId,
      suggestedReplyAvailable: session.suggestedReplyAvailable,
      aiDraftUsed: session.aiDraftUsed,
      fallbackUsed: session.fallbackUsed,
      subject: session.suggestedSubject || `Re: Quotation Proposal for Shipment ${session.shipmentId}`,
      body: session.suggestedBody || '',
      language: session.suggestedLanguage || 'en',
      decision: session.lastDecision || session.status,
      approvedAmount: session.lastApprovedAmount || session.bottomPrice,
      currency: session.currency || DEFAULT_NEGOTIATION_CURRENCY,
      sourceMessageId: session.sourceMessageId || '',
      sourceThreadId: session.sourceThreadId || '',
    };
  }

  /**
   * Validates that LLM generated text respects approved pricing and decision semantics.
   * Handles thousand separators (e.g. $4,520.00 or 4,520 USD) without false rejections.
   */
  private validateAiWording(
    content: string,
    expectedDecision: string,
    approvedAmount: number,
    expectedCurrency: string,
  ): boolean {
    if (!content || content.trim().length === 0) return false;

    // In COUNTER_OFFER or ACCEPT, validate any mentioned prices against approved amount
    if (expectedDecision === 'COUNTER_OFFER' || expectedDecision === 'ACCEPT') {
      const priceRegex = /\$\s*([\d,]+(?:\.\d+)?)|([\d,]+(?:\.\d+)?)\s*USD/gi;
      let match: RegExpExecArray | null;
      while ((match = priceRegex.exec(content)) !== null) {
        const rawNum = match[1] || match[2];
        if (rawNum) {
          const sanitized = rawNum.replace(/,/g, '');
          const foundVal = parseFloat(sanitized);
          if (!isNaN(foundVal)) {
            // If price deviates by more than $1.00 from approved counter offer, reject AI output
            if (Math.abs(foundVal - approvedAmount) > 1.0) {
              return false;
            }
          }
        }
      }
    }

    return true;
  }
}
