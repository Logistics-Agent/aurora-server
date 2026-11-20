import { Injectable, Logger, NotFoundException, BadRequestException, ConflictException, ServiceUnavailableException, Optional } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { PrismaService } from '../../infrastructure/prisma/prisma.service';
import { NegotiationStrategyDomainService, DEFAULT_NEGOTIATION_CURRENCY } from '../../domain/services/negotiation-strategy.domain-service';
import { AiGovernanceNegotiationClient } from '../../infrastructure/grpc/ai-governance.grpc-client';
import { FinancialGrpcClient } from '../../infrastructure/grpc/financial.grpc-client';

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
  quoteId?: string;
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
  quote_id?: string;
}

export interface SuggestedReplyDto {
  subjectSuggestion: string;
  body: string;
  language: string;
}

export interface ProtobufTimestamp {
  seconds: number;
  nanos: number;
}

export interface NegotiationResult {
  sessionId: string;
  shipmentId: string;
  quoteId?: string;
  sourceThreadId?: string;
  round: number;
  decision: string;
  counterOfferPrice?: number;
  approvedAmount: number;
  currency: string;
  aiSpeech: string;
  status: string;
  createdAt: ProtobufTimestamp;
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
    private readonly config: ConfigService,
    @Optional() private readonly financialGrpcClient?: FinancialGrpcClient,
  ) {}

  async submitOffer(input: SubmitOfferInput): Promise<NegotiationResult> {
    const tenantId = this.requireTenant(input.tenantId);
    const shipmentId = (input.shipmentId || input.shipment_id)?.trim() || '';
    const quoteId = (input.quoteId || input.quote_id)?.trim() || undefined;
    const customerId = (input.customerId || input.customer_id)?.trim();
    const offerPrice = input.offerPrice ?? input.offer_price ?? NaN;
    const listPrice = input.listPrice ?? input.list_price ?? NaN;
    const bottomPrice = input.bottomPrice ?? input.bottom_price ?? NaN;
    const customerTier = input.customerTier || input.customer_tier;
    const requestedSessionId = (input.sessionId || input.session_id)?.trim() || undefined;
    const sourceMessageId = (input.sourceMessageId || input.source_message_id)?.trim() || null;
    const sourceThreadId = (input.sourceThreadId || input.source_thread_id)?.trim() || null;

    if (!shipmentId || !customerId || !Number.isFinite(offerPrice) || offerPrice <= 0) {
      throw new BadRequestException('shipmentId, customerId and a positive offerPrice are required');
    }
    const currency = DEFAULT_NEGOTIATION_CURRENCY;

    // 1. Get or create active negotiation session anchored strictly to shipmentId
    let session = requestedSessionId
      ? await this.prisma.negotiationSession.findFirst({
          where: {
            id: requestedSessionId,
            tenantId,
            shipmentId,
            customerId,
          },
        })
      : await this.prisma.negotiationSession.findFirst({
          where: {
            tenantId,
            shipmentId,
            customerId,
            status: 'OPEN',
          },
        });

    if (requestedSessionId && !session) throw new NotFoundException('Negotiation session not found');
    if (session && session.status !== 'OPEN') throw new ConflictException('Negotiation session is closed');
    if (!session) {
      const resolvedPricing = await this.resolveAuthoritativePricing(
        tenantId,
        shipmentId,
        quoteId,
        listPrice,
        bottomPrice,
      );
      session = await this.prisma.negotiationSession.create({
        data: {
          tenantId,
          shipmentId,
          quoteId: quoteId ?? null,
          customerId,
          status: 'OPEN',
          currentRound: 1,
          maxRounds: 5,
          listPrice: resolvedPricing.listPrice,
          bottomPrice: resolvedPricing.bottomPrice,
          currency,
          sourceMessageId,
          sourceThreadId,
        },
      });
    }

    const effectiveShipmentId = shipmentId;
    const effectiveQuoteId = quoteId || session.quoteId || undefined;
    const refForPrompt = effectiveShipmentId;

    // 2. Evaluate Strategy Decision via Deterministic Engine
    const strategyResult = this.strategy.determineDecision({
      offerPrice,
      bottomPrice: session.bottomPrice,
      listPrice: session.listPrice,
      currentRound: session.currentRound,
      maxRounds: session.maxRounds,
      customerTier,
      currency,
      previousApprovedAmount: session.currentRound > 1 ? (session.lastApprovedAmount ?? undefined) : undefined,
    });

    // 3. Generate Wording via AiGovernance under capability 'negotiation.draft'
    const aiResult = await this.aiGovernanceClient.generateNegotiationDraft({
      action: strategyResult.decision,
      approvedAmount: strategyResult.approvedAmount,
      currency: strategyResult.currency,
      customerOffer: offerPrice,
      shipmentId: refForPrompt,
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

    // Validation Guardrails: Check if AI output violates deterministic decision or leaks Cost / Floor B
    const isWordingValid = this.validateAiWording(
      aiResult.content,
      strategyResult.decision,
      strategyResult.approvedAmount,
      strategyResult.currency,
      offerPrice,
      session.bottomPrice,
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
        shipmentId: refForPrompt,
        round: session.currentRound,
      });
      aiDraftUsed = false;
      fallbackUsed = true;
    }

    const suggestedSubject = effectiveShipmentId
      ? `Re: Quotation Proposal for Shipment ${effectiveShipmentId}`
      : `Re: Commercial Quotation Proposal${effectiveQuoteId ? ` (${effectiveQuoteId.slice(0, 8)})` : ''}`;
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

    // 6. ACID Transaction: OCC update + save customer & AI messages cleanly
    const [, savedMsg] = await this.prisma.$transaction(async (tx) => {
      const updated = await tx.negotiationSession.updateMany({
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
          lastApprovedAmount: strategyResult.approvedAmount,
          sourceMessageId: sourceMessageId || session.sourceMessageId,
          sourceThreadId: sourceThreadId || session.sourceThreadId,
        },
      });

      if (updated.count === 0) {
        throw new ConflictException(
          `Negotiation session ${session.id} was modified concurrently or is no longer open at round ${session.currentRound}`,
        );
      }

      const customerMsg = await tx.negotiationMessage.create({
        data: {
          sessionId: session.id,
          round: session.currentRound,
          sender: 'CUSTOMER',
          message: `Customer submitted offer of ${offerPrice} ${strategyResult.currency}`,
          offerPrice,
          decision: 'OFFER_SUBMITTED',
          currency: strategyResult.currency,
        },
      });

      const aiMsg = await tx.negotiationMessage.create({
        data: {
          sessionId: session.id,
          round: session.currentRound,
          sender: 'AI',
          message: finalBody,
          offerPrice: strategyResult.counterOfferPrice || offerPrice,
          decision: strategyResult.decision,
          currency: strategyResult.currency,
        },
      });

      return [customerMsg, aiMsg];
    });

    this.logger.log(
      `[Negotiation] Session ${session.id} | Round ${session.currentRound} | Decision: ${strategyResult.decision} | SuggestedReply Available (AI Used: ${aiDraftUsed}, Fallback Used: ${fallbackUsed})`,
    );

    return {
      sessionId: session.id,
      shipmentId: effectiveShipmentId ?? '',
      quoteId: effectiveQuoteId ?? '',
      sourceThreadId: sourceThreadId || session.sourceThreadId || '',
      round: session.currentRound,
      decision: strategyResult.decision,
      counterOfferPrice: strategyResult.counterOfferPrice,
      approvedAmount: strategyResult.approvedAmount,
      currency: strategyResult.currency,
      aiSpeech: finalBody,
      status: newStatus,
      createdAt: this.toProtobufTimestamp(savedMsg.createdAt),
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

    return {
      sessionId: session.id,
      tenantId: session.tenantId,
      shipmentId: session.shipmentId ?? '',
      quoteId: session.quoteId ?? '',
      sourceThreadId: session.sourceThreadId ?? '',
      customerId: session.customerId,
      status: session.status,
      currentRound: session.currentRound,
      maxRounds: session.maxRounds,
      listPrice: session.listPrice,
      bottomPrice: session.bottomPrice,
      currency: session.currency,
      messages: session.messages.map((m) => ({
        id: m.id,
        sessionId: m.sessionId,
        round: m.round,
        sender: m.sender,
        message: m.message,
        offerPrice: m.offerPrice ?? 0,
        decision: m.decision || '',
        currency: m.currency,
        createdAt: this.toProtobufTimestamp(m.createdAt),
      })),
      createdAt: this.toProtobufTimestamp(session.createdAt),
      updatedAt: this.toProtobufTimestamp(session.updatedAt),
    };
  }

  async listSessions(input: {
    tenantId?: string;
    status?: string;
    page?: number;
    pageSize?: number;
    sourceThreadId?: string;
    shipmentId?: string;
    quoteId?: string;
  }) {
    const tenantId = this.requireTenant(input.tenantId);
    const page = Math.max(1, Number(input.page || 1));
    const pageSize = Math.min(100, Math.max(1, Number(input.pageSize || 20)));
    const where: Record<string, any> = { tenantId };
    if (input.status?.trim()) {
      where.status = input.status.trim().toUpperCase();
    }
    if (input.sourceThreadId?.trim()) {
      where.sourceThreadId = input.sourceThreadId.trim();
    }
    if (input.shipmentId?.trim()) {
      where.shipmentId = input.shipmentId.trim();
    }
    if (input.quoteId?.trim()) {
      where.quoteId = input.quoteId.trim();
    }

    const [sessions, total] = await Promise.all([
      this.prisma.negotiationSession.findMany({
        where,
        orderBy: { updatedAt: 'desc' },
        skip: (page - 1) * pageSize,
        take: pageSize,
      }),
      this.prisma.negotiationSession.count({ where }),
    ]);

    return {
      sessions: sessions.map((s) => this.mapSessionSummary(s)),
      total,
    };
  }

  private mapSessionSummary(s: any) {
    return {
      sessionId: s.id,
      shipmentId: s.shipmentId ?? '',
      quoteId: s.quoteId ?? '',
      sourceThreadId: s.sourceThreadId ?? '',
      customerId: s.customerId,
      status: s.status,
      currentRound: s.currentRound,
      maxRounds: s.maxRounds,
      listPrice: s.listPrice,
      bottomPrice: s.bottomPrice,
      currency: s.currency,
      lastDecision: s.lastDecision || '',
      lastApprovedAmount: s.lastApprovedAmount ?? 0,
      suggestedBody: s.suggestedBody || '',
      suggestedSubject: s.suggestedSubject || '',
      createdAt: this.toProtobufTimestamp(s.createdAt),
      updatedAt: this.toProtobufTimestamp(s.updatedAt),
    };
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
      shipmentId: session.shipmentId ?? '',
      suggestedReplyAvailable: session.suggestedReplyAvailable,
      aiDraftUsed: session.aiDraftUsed,
      fallbackUsed: session.fallbackUsed,
      subject:
        session.suggestedSubject ||
        (session.shipmentId
          ? `Re: Quotation Proposal for Shipment ${session.shipmentId}`
          : 'Re: Commercial Quotation Proposal'),
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
   * Resolves authoritative listPrice & bottomPrice from financial-service when configured,
   * or validates caller-supplied pricing in non-production environments.
   */
  private async resolveAuthoritativePricing(
    tenantId: string,
    shipmentId: string | undefined,
    quoteId: string | undefined,
    inputListPrice: number,
    inputBottomPrice: number,
  ): Promise<{ listPrice: number; bottomPrice: number }> {
    if (this.financialGrpcClient?.isConfigured()) {
      const grpcPricing = await this.financialGrpcClient.resolveQuotePricing(
        tenantId,
        shipmentId,
        quoteId,
      );
      if (grpcPricing) {
        return grpcPricing;
      }
    }

    const financialServiceUrl = this.config.get<string>('FINANCIAL_SERVICE_URL');
    if (financialServiceUrl?.trim()) {
      const baseUrl = financialServiceUrl.trim().replace(/\/+$/, '');
      if (quoteId) {
        try {
          const controller = new AbortController();
          const timer = setTimeout(() => controller.abort(), 2500);
          const res = await fetch(
            `${baseUrl}/api/v1/financial/quotes?quoteId=${encodeURIComponent(quoteId)}`,
            {
              headers: { 'x-tenant-id': tenantId, Accept: 'application/json' },
              signal: controller.signal,
            },
          );
          clearTimeout(timer);
          if (res.ok) {
            const payload = (await res.json()) as Record<string, any>;
            const quotes = payload?.quotes ?? payload?.data?.quotes ?? [];
            const quoteData = Array.isArray(quotes) ? quotes[0] : quotes;
            const remoteList = Number(quoteData?.targetRate ?? quoteData?.listPrice);
            const remoteBottom = Number(quoteData?.minAcceptableRate ?? quoteData?.bottomPrice);
            if (
              Number.isFinite(remoteList) &&
              Number.isFinite(remoteBottom) &&
              remoteList > 0 &&
              remoteBottom > 0 &&
              remoteBottom <= remoteList
            ) {
              return { listPrice: remoteList, bottomPrice: remoteBottom };
            }
          }
        } catch (err) {
          this.logger.warn(
            `[Negotiation] Failed to fetch quote ${quoteId} from FINANCIAL_SERVICE_URL: ${(err as Error).message}`,
          );
        }
      }
      if (shipmentId) {
        try {
          const controller = new AbortController();
          const timer = setTimeout(() => controller.abort(), 2500);
          const res = await fetch(
            `${baseUrl}/api/v1/financial/quotes/${encodeURIComponent(shipmentId)}`,
            {
              headers: { 'x-tenant-id': tenantId, Accept: 'application/json' },
              signal: controller.signal,
            },
          );
          clearTimeout(timer);
          if (res.ok) {
            const payload = (await res.json()) as Record<string, any>;
            const quoteData = payload?.data ?? payload;
            const remoteList = Number(quoteData?.listPrice ?? quoteData?.finalPrice ?? quoteData?.targetRate);
            const remoteBottom = Number(quoteData?.bottomPrice ?? quoteData?.floorPrice ?? quoteData?.minAcceptableRate);
            if (
              Number.isFinite(remoteList) &&
              Number.isFinite(remoteBottom) &&
              remoteList > 0 &&
              remoteBottom > 0 &&
              remoteBottom <= remoteList
            ) {
              return { listPrice: remoteList, bottomPrice: remoteBottom };
            }
          }
        } catch (err) {
          this.logger.warn(
            `[Negotiation] Failed to fetch authoritative pricing from FINANCIAL_SERVICE_URL for shipment ${shipmentId}: ${(err as Error).message}`,
          );
        }
      }
    }

    if (this.config.get<string>('NODE_ENV') === 'production') {
      throw new ServiceUnavailableException(
        'Authoritative shipment pricing is not connected; a negotiation session cannot be opened',
      );
    }

    if (
      !Number.isFinite(inputListPrice) ||
      !Number.isFinite(inputBottomPrice) ||
      inputListPrice <= 0 ||
      inputBottomPrice <= 0 ||
      inputBottomPrice > inputListPrice
    ) {
      throw new BadRequestException('Valid listPrice and bottomPrice are required to open a session');
    }

    return { listPrice: inputListPrice, bottomPrice: inputBottomPrice };
  }

  /**
   * Validates that LLM generated text respects approved pricing and decision semantics.
   * Handles thousand separators and prefix/suffix currency formats ($4,520.00, USD 4,520, or 4,520 USD),
   * allows referencing the customer's offer alongside the approved counter-offer, and blocks
   * contradictory acceptance statements during a COUNTER_OFFER.
   */
  private validateAiWording(
    content: string,
    expectedDecision: string,
    approvedAmount: number,
    expectedCurrency: string,
    customerOffer?: number,
    bottomPrice?: number,
  ): boolean {
    if (!content || content.trim().length === 0) return false;

    // Never allow internal cost, margin, or bottom/floor price terminology in outbound customer wording
    if (
      /\b(base cost|cost price|floor price|bottom price|minimum acceptable|internal margin)\b|(giá vốn|giá sàn)/i.test(
        content,
      )
    ) {
      return false;
    }

    if (
      expectedDecision === 'COUNTER_OFFER' &&
      /\b(your offer (has been|is) accepted|we accept your offer|pleased to accept your offer|deal is accepted)\b/i.test(content)
    ) {
      return false;
    }

    // In COUNTER_OFFER or ACCEPT, validate any mentioned prices against approvedAmount (and optional customerOffer echo)
    if (expectedDecision === 'COUNTER_OFFER' || expectedDecision === 'ACCEPT') {
      const priceRegex =
        /\$\s*([\d,]+(?:\.\d+)?)|USD\s*([\d,]+(?:\.\d+)?)|([\d,]+(?:\.\d+)?)\s*USD/gi;
      const mentionedPrices: number[] = [];
      let match: RegExpExecArray | null;
      while ((match = priceRegex.exec(content)) !== null) {
        const rawNum = match[1] || match[2] || match[3];
        if (rawNum) {
          const sanitized = rawNum.replace(/,/g, '');
          const foundVal = parseFloat(sanitized);
          if (!isNaN(foundVal)) {
            mentionedPrices.push(foundVal);
          }
        }
      }

      if (
        expectedDecision === 'COUNTER_OFFER' &&
        bottomPrice !== undefined &&
        Math.abs(approvedAmount - bottomPrice) > 1.0 &&
        mentionedPrices.some((val) => Math.abs(val - bottomPrice) <= 1.0)
      ) {
        return false;
      }

      if (mentionedPrices.length > 0) {
        const hasApprovedAmount = mentionedPrices.some(
          (val) => Math.abs(val - approvedAmount) <= 1.0,
        );
        const allAllowed = mentionedPrices.every(
          (val) =>
            Math.abs(val - approvedAmount) <= 1.0 ||
            (customerOffer !== undefined && Math.abs(val - customerOffer) <= 1.0),
        );
        if (!hasApprovedAmount || !allAllowed) {
          return false;
        }
      }
    }

    return true;
  }

  private toProtobufTimestamp(date: Date): ProtobufTimestamp {
    const ms = date.getTime();
    return {
      seconds: Math.floor(ms / 1000),
      nanos: (ms % 1000) * 1_000_000,
    };
  }

  private requireTenant(tenantId?: string): string {
    if (!tenantId?.trim()) throw new BadRequestException('tenantId is required');
    return tenantId.trim();
  }
}
