import { Injectable } from '@nestjs/common';

export type NegotiationDecision = 'ACCEPT' | 'COUNTER_OFFER' | 'HUMAN_HANDOFF' | 'REJECT';

export const DEFAULT_NEGOTIATION_CURRENCY = 'USD';

export interface StrategyInput {
  offerPrice: number;
  bottomPrice: number;
  listPrice: number;
  currentRound: number;
  maxRounds: number;
  customerTier?: string; // 'STANDARD' | 'VIP' | 'ENTERPRISE'
  currency?: string;
  previousApprovedAmount?: number;
}

export interface StrategyResult {
  decision: NegotiationDecision;
  approvedAmount: number;
  counterOfferPrice?: number;
  currency: string;
  reason: string;
}

/**
 * NegotiationStrategyDomainService
 *
 * Deterministic Engine (NestJS): AI Safety & Deterministic Guardrails.
 * LLM KHÔNG ĐƯỢC TỰ QUYẾT ĐỊNH GIÁ TIỀN mà chỉ diễn đạt ngôn ngữ tự nhiên.
 * Tất cả con số/tài chính được tính toán hoàn toàn bởi Engine này theo
 * mô hình Boulware + Reciprocity Concession Curve:
 * - Chặn trả giá phá rối (Lowball Anchoring): Không bao giờ lộ giá đáy ở vòng đầu khi khách trả giá quá thấp.
 * - Tương hỗ (Reciprocity): Khách trả giá càng sát bottomPrice, hệ thống càng mở khóa biên độ nhượng bộ hợp lý.
 * - Đơn điệu theo vòng (Strict Monotonicity): Khi khách thiện chí nâng giá ở các vòng sau, giá báo lại giảm dần về gần bottomPrice; nếu khách hạ giá, hệ thống giữ nguyên mức giá vòng trước.
 */
@Injectable()
export class NegotiationStrategyDomainService {
  determineDecision(input: StrategyInput): StrategyResult {
    const { offerPrice, bottomPrice, listPrice, currentRound, maxRounds, customerTier } = input;
    const currency = input.currency || DEFAULT_NEGOTIATION_CURRENCY;

    // Rule 1: VIP/ENTERPRISE customer tier -> Automatic Human Handoff for personalized service
    if (customerTier === 'VIP' || customerTier === 'ENTERPRISE') {
      return {
        decision: 'HUMAN_HANDOFF',
        approvedAmount: offerPrice,
        currency,
        reason: `Customer tier '${customerTier}' requires personal account manager negotiation.`,
      };
    }

    // Rule 2: Offer meets or exceeds bottom acceptable price -> ACCEPT
    if (offerPrice >= bottomPrice) {
      return {
        decision: 'ACCEPT',
        approvedAmount: offerPrice,
        counterOfferPrice: offerPrice,
        currency,
        reason: `Offer price $${offerPrice} is above bottom price $${bottomPrice}. Deal accepted!`,
      };
    }

    // Rule 3: Max negotiation rounds reached -> HUMAN_HANDOFF to sales agent
    if (currentRound >= maxRounds) {
      return {
        decision: 'HUMAN_HANDOFF',
        approvedAmount: bottomPrice,
        currency,
        reason: `Maximum negotiation rounds (${maxRounds}) reached. Escalate to human sales agent.`,
      };
    }

    // Rule 4: Boulware + Reciprocity Concession Curve
    const negotiableMargin = Math.max(0, listPrice - bottomPrice);
    if (negotiableMargin === 0) {
      return {
        decision: 'COUNTER_OFFER',
        approvedAmount: bottomPrice,
        counterOfferPrice: bottomPrice,
        currency,
        reason: `List price equals bottom price at $${bottomPrice}.`,
      };
    }

    // Measure customer goodwill alpha in [0.15, 0.75] based on proximity of offerPrice to bottomPrice
    const rawGoodwill = (offerPrice - (bottomPrice - negotiableMargin)) / negotiableMargin;
    const alpha = Math.min(0.75, Math.max(0.15, rawGoodwill));

    let targetCounter: number;
    const prevApproved = input.previousApprovedAmount;

    if (currentRound > 1 && prevApproved !== undefined && prevApproved >= bottomPrice) {
      // In Round 2+:
      // - If customer lowballs (rawGoodwill < 0.5), hold previous counter-offer firm.
      // - If customer negotiates in good faith (rawGoodwill >= 0.5), concede a bounded step toward bottomPrice.
      if (rawGoodwill < 0.5) {
        targetCounter = prevApproved;
      } else {
        const remainingMargin = Math.max(0, prevApproved - bottomPrice);
        const roundFactor = Math.min(1, (currentRound - 1) / Math.max(1, maxRounds - 1));
        const stepConcession = remainingMargin * alpha * roundFactor;
        targetCounter = prevApproved - stepConcession;
      }
    } else {
      // Round 1: Anchor from listPrice based on customer goodwill alpha (never exposes bottomPrice on Round 1)
      targetCounter = listPrice - alpha * negotiableMargin;
    }

    const counterOfferPrice = Number(
      Math.min(listPrice, Math.max(bottomPrice, targetCounter)).toFixed(2),
    );

    return {
      decision: 'COUNTER_OFFER',
      approvedAmount: counterOfferPrice,
      counterOfferPrice,
      currency,
      reason: `Offer price $${offerPrice} is below bottom price $${bottomPrice}. Proposed counter offer at $${counterOfferPrice}.`,
    };
  }
}
