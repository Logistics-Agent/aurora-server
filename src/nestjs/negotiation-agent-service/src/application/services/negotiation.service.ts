import { Injectable, Logger, NotFoundException, BadRequestException, ConflictException, ServiceUnavailableException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { PrismaService } from '../../infrastructure/prisma/prisma.service';
import { NegotiationStrategyDomainService, DEFAULT_NEGOTIATION_CURRENCY } from '../../domain/services/negotiation-strategy.domain-service';
import { AiDraftGenerateInput, AiGovernanceNegotiationClient } from '../../infrastructure/grpc/ai-governance.grpc-client';

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
  suggestedAmount: number;
  currency: string;
  aiSpeech: string;
  status: string;
  createdAt: { seconds: number; nanos: number };
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
  suggestedAmount: number;
  round: number;
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
    private readonly config: ConfigService,
  ) {}

  async submitOffer(input: SubmitOfferInput): Promise<NegotiationResult> {
    const tenantId = this.requireTenant(input.tenantId);
    const shipmentId = input.shipmentId || input.shipment_id;
    const customerId = input.customerId || input.customer_id;
    const offerPrice = input.offerPrice ?? input.offer_price ?? NaN;
    const listPrice = input.listPrice ?? input.list_price ?? NaN;
    const bottomPrice = input.bottomPrice ?? input.bottom_price ?? NaN;
    if (!shipmentId || !customerId || !Number.isFinite(offerPrice) || offerPrice <= 0) {
      throw new BadRequestException('shipmentId, customerId and a positive offerPrice are required');
    }
    const customerTier = input.customerTier || input.customer_tier;
    const requestedSessionId = input.sessionId || input.session_id;
    const sourceMessageId = (input.sourceMessageId || input.source_message_id || '').trim();
    const sourceThreadId = input.sourceThreadId || input.source_thread_id || null;
    const currency = DEFAULT_NEGOTIATION_CURRENCY;
    if (!sourceMessageId) {
      throw new BadRequestException('sourceMessageId is required to identify each customer offer');
    }
    if (!this.isMoneyAmount(offerPrice)) {
      throw new BadRequestException('offerPrice must be a positive USD amount with at most two decimal places');
    }

    // 1. Get or create active negotiation session
    let session = requestedSessionId
      ? await this.prisma.negotiationSession.findFirst({ where: { id: requestedSessionId, tenantId, shipmentId, customerId } })
      : await this.prisma.negotiationSession.findFirst({
          where: {
            tenantId,
            shipmentId,
            customerId,
            status: { in: ['OPEN', 'PENDING_APPROVAL'] },
          },
        });

    if (requestedSessionId && !session) throw new NotFoundException('Negotiation session not found');
    if (session && session.status !== 'OPEN') throw new ConflictException('Negotiation is awaiting staff action');
    if (session && this.config.get<string>('NODE_ENV') === 'production' &&
        (!session.pricingEvidenceReference || !session.pricingApprovedBy || !session.pricingApprovedAt)) {
      throw new ServiceUnavailableException('Negotiation session has no approved pricing evidence');
    }
    if (!session) {
      if (this.config.get<string>('NODE_ENV') === 'production') {
        throw new ServiceUnavailableException('Authoritative shipment pricing is not connected; a negotiation session cannot be opened');
      }
      if (!this.isMoneyAmount(listPrice) || !this.isMoneyAmount(bottomPrice) ||
          bottomPrice > listPrice) {
        throw new BadRequestException('Valid listPrice and bottomPrice are required to open a session');
      }
      try {
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
      } catch (error) {
        if (this.isUniqueConflict(error)) {
          throw new ConflictException('An active negotiation already exists for this shipment and customer');
        }
        throw error;
      }
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
    const draftInput: AiDraftGenerateInput = {
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
    };
    const aiResult = strategyResult.decision === 'HUMAN_HANDOFF' || strategyResult.decision === 'REJECT'
      ? {
          content: this.aiGovernanceClient.getDeterministicFallback(draftInput),
          isFallback: true,
        }
      : await this.aiGovernanceClient.generateNegotiationDraft(draftInput);

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
      offerPrice,
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
      newStatus = 'PENDING_APPROVAL';
    } else if (strategyResult.decision === 'HUMAN_HANDOFF') {
      newStatus = 'HANDOFF';
    } else if (strategyResult.decision === 'REJECT') {
      newStatus = 'REJECTED';
    }

    // 6. ACID Transaction: Save message + update session round/status/suggestedReply
    let savedMsg: { createdAt: Date };
    try {
      savedMsg = await this.prisma.$transaction(async (tx) => {
        const claimed = await tx.negotiationSession.updateMany({
          where: {
            id: session.id,
            tenantId,
            status: 'OPEN',
            currentRound: session.currentRound,
          },
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
            lastSuggestedAmount: strategyResult.decision === 'HUMAN_HANDOFF' ? null : strategyResult.approvedAmount,
            sourceMessageId,
            sourceThreadId: sourceThreadId || session.sourceThreadId,
          },
        });
        if (claimed.count !== 1) {
          throw new ConflictException('Negotiation changed while this offer was being processed');
        }
        return tx.negotiationMessage.create({
          data: {
            sessionId: session.id,
            round: session.currentRound,
            sender: 'AI',
            message: finalBody,
            offerPrice: strategyResult.counterOfferPrice ?? null,
            customerOfferPrice: offerPrice,
            sourceMessageId,
            decision: strategyResult.decision,
            currency: strategyResult.currency,
          },
        });
      });
    } catch (error) {
      if (this.isUniqueConflict(error)) throw new ConflictException('This customer offer has already been processed');
      throw error;
    }

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
      suggestedAmount: strategyResult.approvedAmount,
      currency: strategyResult.currency,
      aiSpeech: finalBody,
      status: newStatus,
      createdAt: { seconds: Math.floor(savedMsg.createdAt.getTime() / 1000), nanos: 0 },
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

  async getSessionHistory(sessionId: string, tenantId?: string) {
    const session = await this.prisma.negotiationSession.findFirst({
      where: { id: sessionId, tenantId: this.requireTenant(tenantId) },
      include: { messages: { orderBy: { createdAt: 'asc' } } },
    });

    if (!session) {
      throw new NotFoundException(`Negotiation Session ${sessionId} not found`);
    }

    return session;
  }

  async getDraftSuggestion(sessionId: string, tenantId?: string): Promise<DraftSuggestionResult> {
    const session = await this.prisma.negotiationSession.findFirst({
      where: { id: sessionId, tenantId: this.requireTenant(tenantId) },
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
      approvedAmount: session.lastSuggestedAmount ?? 0,
      suggestedAmount: session.lastSuggestedAmount ?? 0,
      round: Math.max(0, session.currentRound - 1),
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
    customerOffer: number,
  ): boolean {
    if (!content || content.trim().length === 0) return false;

    // In COUNTER_OFFER or ACCEPT, validate any mentioned prices against approved amount
    if (/\b(accepted|confirmed|agreement finalized|deal closed|we accept|we agree)\b|đã chốt|đã xác nhận|chấp nhận đề xuất/iu.test(content)) {
      return false;
    }
    if (expectedDecision === 'ACCEPT' &&
        /\b(reject|decline|counter.?offer|cannot proceed)\b|từ chối|không thể tiếp tục/iu.test(content)) {
      return false;
    }
    if (expectedDecision === 'COUNTER_OFFER' || expectedDecision === 'ACCEPT') {
      const priceRegex = /\$\s*([\d,]+(?:\.\d+)?)|([\d,]+(?:\.\d+)?)\s*(USD|EUR|VND)\b/gi;
      let match: RegExpExecArray | null;
      let mentionsSuggestedAmount = false;
      while ((match = priceRegex.exec(content)) !== null) {
        const rawNum = match[1] || match[2];
        if (match[3] && match[3].toUpperCase() !== expectedCurrency) return false;
        const trailingCurrency = content.slice(priceRegex.lastIndex).match(/^\s*(USD|EUR|VND)\b/i)?.[1];
        if (trailingCurrency && trailingCurrency.toUpperCase() !== expectedCurrency) return false;
        if (rawNum) {
          const sanitized = rawNum.replace(/,/g, '');
          const foundVal = parseFloat(sanitized);
          if (!isNaN(foundVal)) {
            // Reject every unrecognized monetary amount; no tolerance beyond half a cent.
            if (Math.abs(foundVal - approvedAmount) < 0.005) {
              mentionsSuggestedAmount = true;
            } else if (Math.abs(foundVal - customerOffer) >= 0.005) {
              return false;
            }
          }
        }
      }
      if (!mentionsSuggestedAmount) return false;
    }

    return true;
  }

  private isMoneyAmount(value: number): boolean {
    return Number.isFinite(value) && value > 0 &&
      Number.isSafeInteger(Math.round(value * 100)) &&
      Math.abs(value * 100 - Math.round(value * 100)) < 0.000001;
  }

  private isUniqueConflict(error: unknown): boolean {
    return typeof error === 'object' && error !== null &&
      'code' in error && error.code === 'P2002';
  }

  private requireTenant(tenantId?: string): string {
    if (!tenantId?.trim()) throw new BadRequestException('tenantId is required');
    return tenantId.trim();
  }
}
