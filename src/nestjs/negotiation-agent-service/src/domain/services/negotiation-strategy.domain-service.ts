import { Injectable } from '@nestjs/common';

export type NegotiationDecision = 'ACCEPT' | 'COUNTER_OFFER' | 'HUMAN_HANDOFF' | 'REJECT';

export interface StrategyInput {
  offerPrice: number;
  bottomPrice: number;
  listPrice: number;
  currentRound: number;
  maxRounds: number;
  customerTier?: string; // 'STANDARD' | 'VIP' | 'ENTERPRISE'
}

export interface StrategyResult {
  decision: NegotiationDecision;
  counterOfferPrice?: number;
  reason: string;
}

/**
 * NegotiationStrategyDomainService
 *
 * Deterministic Engine (NestJS): AI Safety & Deterministic Guardrails.
 * AI Gemini KHÔNG ĐƯỢC TỰ QUYẾT ĐỊNH GIÁ TIỀN mà chỉ diễn đạt ngôn ngữ tự nhiên.
 * Tất cả con số/tài chính được tính toán hoàn toàn bởi Engine này.
 */
@Injectable()
export class NegotiationStrategyDomainService {
  determineDecision(input: StrategyInput): StrategyResult {
    const { offerPrice, bottomPrice, listPrice, currentRound, maxRounds, customerTier } = input;

    // Rule 1: VIP/ENTERPRISE customer tier -> Automatic Human Handoff for personalized service
    if (customerTier === 'VIP' || customerTier === 'ENTERPRISE') {
      return {
        decision: 'HUMAN_HANDOFF',
        reason: `Customer tier '${customerTier}' requires personal account manager negotiation.`,
      };
    }

    // Rule 2: Offer meets or exceeds bottom acceptable price -> ACCEPT
    if (offerPrice >= bottomPrice) {
      return {
        decision: 'ACCEPT',
        counterOfferPrice: offerPrice,
        reason: `Offer price $${offerPrice} is above bottom price $${bottomPrice}. Deal accepted!`,
      };
    }

    // Rule 3: Max negotiation rounds reached -> HUMAN_HANDOFF to sales agent
    if (currentRound >= maxRounds) {
      return {
        decision: 'HUMAN_HANDOFF',
        reason: `Maximum negotiation rounds (${maxRounds}) reached. Escalate to human sales agent.`,
      };
    }

    // Rule 4: Offer is below bottom price and rounds available -> COUNTER_OFFER
    // Step formula: Counter = Max(bottomPrice, offerPrice + (listPrice - offerPrice) * 0.4)
    const rawCounter = offerPrice + (listPrice - offerPrice) * 0.4;
    const counterOfferPrice = Number(Math.max(bottomPrice, rawCounter).toFixed(2));

    return {
      decision: 'COUNTER_OFFER',
      counterOfferPrice,
      reason: `Offer price $${offerPrice} is below bottom price $${bottomPrice}. Proposed counter offer at $${counterOfferPrice}.`,
    };
  }
}
