import { Injectable, Logger } from '@nestjs/common';
import { AssistantIntent } from '../../domain/enums/assistant-intent.enum';
import { NeedRagDecision } from '../../domain/enums/need-rag-decision.enum';
import { AiGovernanceGrpcClient } from '../../infrastructure/grpc/ai-governance.grpc-client';
import { CurrentUser } from '../../infrastructure/security/current-user.interface';

export interface IntentClassificationResult {
  intent: AssistantIntent;
  decision: NeedRagDecision;
  confidence: number;
  extractedParameters?: Record<string, any>;
}

@Injectable()
export class IntentRouterService {
  private readonly logger = new Logger(IntentRouterService.name);

  constructor(private readonly aiGovernanceClient: AiGovernanceGrpcClient) {}

  async classify(query: string, context: CurrentUser): Promise<IntentClassificationResult> {
    const qLower = query.toLowerCase().trim();
    const uuidPattern = '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}';

    // ==========================================
    // Layer 1: Deterministic Fast-Path Rules
    // ==========================================

    // 1. Simple greetings / small-talk -> NO_RAG
    if (
      /^(xin chào|chào|hello|hi|good morning|hey|alo|bạn là ai|who are you|help|cần trợ giúp)$/i.test(qLower) ||
      (qLower.length < 15 && (qLower.includes('chào') || qLower.includes('hello') || qLower.includes('hi')))
    ) {
      return {
        intent: AssistantIntent.GENERAL,
        decision: NeedRagDecision.NO_RAG,
        confidence: 1.0,
      };
    }

    const targetCustomerId = query.match(
      new RegExp(`(?:khách hàng|customer)\\s*(?:id|mã|số)?\\s*[:#-]?\\s*(${uuidPattern})`, 'i'),
    )?.[1];
    const firstUuid = query.match(new RegExp(uuidPattern, 'i'))?.[0];
    const shipmentId = firstUuid === targetCustomerId ? undefined : firstUuid;
    const shipmentNo = query.match(/shp-[a-zA-Z0-9_-]+/i)?.[0];

    const mentionsBilling =
      qLower.includes('công nợ') ||
      qLower.includes('hóa đơn') ||
      qLower.includes('invoice') ||
      qLower.includes('tiền nợ') ||
      qLower.includes('thanh toán') ||
      qLower.includes('số dư');

    const mentionsShipmentTracking =
      Boolean(shipmentNo) ||
      Boolean(shipmentId) ||
      qLower.includes('ở đâu') ||
      qLower.includes('vị trí') ||
      qLower.includes('theo dõi');

    // 2. Hybrid / SOP / Regulatory queries (supports cross-domain execution if shipment/billing is also mentioned)
    const mentionsLaw =
      qLower.includes('luật') ||
      qLower.includes('nghị định') ||
      qLower.includes('thông tư') ||
      qLower.includes('hải quan') ||
      qLower.includes('quy định pháp lý') ||
      qLower.includes('customs regulation');

    const mentionsSop =
      qLower.includes('sop') ||
      qLower.includes('quy trình nội bộ') ||
      qLower.includes('hướng dẫn nội bộ') ||
      qLower.includes('hợp đồng') ||
      qLower.includes('công ty chúng tôi');

    const mentionsRegulatoryTopic =
      mentionsLaw ||
      qLower.includes('thủ tục') ||
      qLower.includes('chứng từ') ||
      qLower.includes('hs code') ||
      qLower.includes('thuế') ||
      qLower.includes('dangerous goods') ||
      qLower.includes('hàng nguy hiểm') ||
      qLower.includes('pin lithium') ||
      qLower.includes('xuất khẩu') ||
      qLower.includes('nhập khẩu');

    const crossDomainParams = {
      targetCustomerId,
      shipmentId,
      shipmentNo,
      includeShipmentLookup: mentionsShipmentTracking,
      includeBillingSummary: mentionsBilling,
    };

    if (mentionsLaw && mentionsSop) {
      return {
        intent: AssistantIntent.HYBRID_RAG_QUERY,
        decision: NeedRagDecision.RAG_HYBRID,
        confidence: 0.9,
        extractedParameters: crossDomainParams,
      };
    }

    if (mentionsSop) {
      return {
        intent: AssistantIntent.KNOWLEDGE_QUERY,
        decision: NeedRagDecision.RAG_KNOWLEDGE,
        confidence: 0.85,
        extractedParameters: crossDomainParams,
      };
    }

    if (mentionsRegulatoryTopic) {
      return {
        intent: AssistantIntent.REGULATORY_QUERY,
        decision: NeedRagDecision.RAG_REGULATORY,
        confidence: 0.85,
        extractedParameters: crossDomainParams,
      };
    }

    // 3. Billing & Debt queries (supports parallel multi-tool execution if shipment tracking is also requested)
    if (mentionsBilling) {
      return {
        intent: AssistantIntent.BILLING_QUERY,
        decision: NeedRagDecision.DOMAIN_QUERY,
        confidence: 0.95,
        extractedParameters: crossDomainParams,
      };
    }

    // 4. Shipment queries -> DOMAIN_QUERY
    if (
      qLower.includes('vận đơn') ||
      qLower.includes('lô hàng') ||
      qLower.includes('shipment') ||
      mentionsShipmentTracking
    ) {
      return {
        intent: AssistantIntent.SHIPMENT_QUERY,
        decision: NeedRagDecision.DOMAIN_QUERY,
        confidence: 0.95,
        extractedParameters: {
          shipmentId,
          shipmentNo,
          targetCustomerId,
        },
      };
    }

    // ==========================================
    // Layer 2: Fast AI Classifier Fallback
    // ==========================================
    try {
      const prompt = `Classify the user logistics/customs query into exactly one category:
- GENERAL (general conversation, greeting)
- SHIPMENT_QUERY (status, location of specific shipment)
- BILLING_QUERY (invoices, debt, balance)
- REGULATORY_QUERY (customs laws, decrees, export/import requirements)
- KNOWLEDGE_QUERY (internal company SOPs, carrier guidelines)
- HYBRID_RAG_QUERY (both external regulations and internal SOPs)

Output strictly JSON: {"intent": "CATEGORY"}

User Query: "${query}"`;

      const aiRes = await this.aiGovernanceClient.generate('assistant.route', prompt, context, 64);
      const cleaned = aiRes.content.replace(/```(?:json)?/g, '').replace(/```/g, '').trim();
      const parsed = JSON.parse(cleaned);
      const intentStr = (parsed.intent || 'GENERAL').toUpperCase();

      let intent = AssistantIntent.GENERAL;
      let decision = NeedRagDecision.NO_RAG;

      if (intentStr === 'SHIPMENT_QUERY') {
        intent = AssistantIntent.SHIPMENT_QUERY;
        decision = NeedRagDecision.DOMAIN_QUERY;
      } else if (intentStr === 'BILLING_QUERY') {
        intent = AssistantIntent.BILLING_QUERY;
        decision = NeedRagDecision.DOMAIN_QUERY;
      } else if (intentStr === 'REGULATORY_QUERY') {
        intent = AssistantIntent.REGULATORY_QUERY;
        decision = NeedRagDecision.RAG_REGULATORY;
      } else if (intentStr === 'KNOWLEDGE_QUERY') {
        intent = AssistantIntent.KNOWLEDGE_QUERY;
        decision = NeedRagDecision.RAG_KNOWLEDGE;
      } else if (intentStr === 'HYBRID_RAG_QUERY') {
        intent = AssistantIntent.HYBRID_RAG_QUERY;
        decision = NeedRagDecision.RAG_HYBRID;
      }

      return { intent, decision, confidence: 0.75 };
    } catch (error) {
      this.logger.warn(`AI Router fallback failed, defaulting to HYBRID_RAG: ${(error as Error).message}`);
      return {
        intent: AssistantIntent.HYBRID_RAG_QUERY,
        decision: NeedRagDecision.RAG_HYBRID,
        confidence: 0.5,
      };
    }
  }
}
