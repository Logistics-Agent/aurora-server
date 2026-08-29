import { Injectable, OnModuleInit, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as grpc from '@grpc/grpc-js';
import * as protoLoader from '@grpc/proto-loader';
import * as path from 'path';

export type NegotiationDecision = 'ACCEPT' | 'COUNTER_OFFER' | 'HUMAN_HANDOFF' | 'REJECT';

export interface GenerateSpeechInput {
  decision: NegotiationDecision;
  offerPrice: number;
  counterOfferPrice?: number;
  shipmentId: string;
  round: number;
  bottomPrice?: number;
  listPrice?: number;
  businessReason?: string;
  context?: {
    tenantId?: string;
    userId?: string;
    traceId?: string;
    correlationId?: string;
  };
}

export interface NegotiationSpeechResult {
  speech: string;
  action: NegotiationDecision;
  amount?: number;
  currency: string;
  aiDraftUsed: boolean;
  fallbackUsed: boolean;
  decisionId?: string;
}

interface StructuredLlmOutput {
  action?: string;
  currency?: string;
  amount?: number;
  message?: string;
}

@Injectable()
export class AiGovernanceNegotiationClient implements OnModuleInit {
  private readonly logger = new Logger(AiGovernanceNegotiationClient.name);
  private client: any;

  constructor(private readonly configService: ConfigService) {}

  onModuleInit() {
    const protoPath = path.resolve(__dirname, '../../../../../../protos/ai_governance.proto');
    const grpcUrl = this.configService.get<string>('AI_GOVERNANCE_GRPC_URL') || 'localhost:9090';

    try {
      const packageDefinition = protoLoader.loadSync(protoPath, {
        keepCase: true,
        longs: String,
        enums: String,
        defaults: true,
        oneofs: true,
      });

      const protoDescriptor = grpc.loadPackageDefinition(packageDefinition) as any;
      const service = protoDescriptor.ai_governance.AiExecutionService;
      this.client = new service(grpcUrl, grpc.credentials.createInsecure());
      this.logger.log(`[AiGovernanceNegotiationClient] Connected to AiGovernance at ${grpcUrl}`);
    } catch (err: any) {
      this.logger.warn(
        `[AiGovernanceNegotiationClient] Failed to load proto from ${protoPath}. Initializing with safe deterministic fallback.`,
        err,
      );
    }
  }

  /**
   * Generates natural language speech response for negotiation via AiGovernance.Generate.
   * Enforces strict deterministic domain decision & financial amount guardrails.
   */
  async generateNegotiationSpeech(input: GenerateSpeechInput): Promise<NegotiationSpeechResult> {
    const expectedAmount = input.decision === 'COUNTER_OFFER'
      ? (input.counterOfferPrice ?? input.offerPrice)
      : input.offerPrice;

    if (!this.client) {
      this.logger.warn('[AiGovernanceNegotiationClient] gRPC client not initialized. Using deterministic fallback.');
      return this.createFallbackResult(input, expectedAmount, 'Client uninitialized');
    }

    const tenantId = input.context?.tenantId;
    const userId = input.context?.userId;
    const traceId = input.context?.traceId;
    const correlationId = input.context?.correlationId;

    const metadata = new grpc.Metadata();
    metadata.add('x-service-id', 'negotiation-agent-service');
    if (tenantId) metadata.add('x-tenant-id', tenantId);
    if (userId) metadata.add('x-user-id', userId);
    if (traceId) metadata.add('x-trace-id', traceId);
    if (correlationId) metadata.add('x-correlation-id', correlationId);

    const systemInstruction = `You are an automated logistics negotiation agent for Aurora Platform.
Your task is to draft a polite, professional, and concise natural-language response (in English/ Vietnamese when needed by customer) explaining the negotiation decision to the customer.

RULES & GUARDRAILS:
1. The supplied action and financial amounts are authoritative, final, and non-negotiable.
2. DO NOT change or invent decisions, prices, discounts, currencies, or promises.
3. If action is ACCEPT, confirm acceptance at the exact customer offer price.
4. If action is COUNTER_OFFER, offer the exact approved counter-offer price.
5. If action is HUMAN_HANDOFF, explain that a senior account manager will assist.
6. If action is REJECT, politely decline without accepting.
7. Return ONLY valid JSON adhering strictly to this schema:
{
  "action": "${input.decision}",
  "currency": "USD",
  "amount": ${expectedAmount},
  "message": "<English text message>"
}`;

    const promptData = {
      action: input.decision,
      currency: 'USD',
      customer_offer: input.offerPrice,
      approved_counter_offer: input.counterOfferPrice,
      shipment_id: input.shipmentId,
      negotiation_round: input.round,
      business_reason: input.businessReason,
    };

    const fullPrompt = `${systemInstruction}\n\n[NEGOTIATION CONTEXT]\n${JSON.stringify(promptData, null, 2)}`;

    const request = {
      capability_code: 'negotiation.draft',
      prompt: fullPrompt,
      max_output_tokens: 1024,
      estimated_input_tokens: Math.max(10, Math.floor(fullPrompt.length / 4)),
    };

    try {
      const response: any = await new Promise((resolve, reject) => {
        this.client.Generate(request, metadata, { deadline: Date.now() + 15000 }, (err: any, res: any) => {
          if (err) return reject(err);
          resolve(res);
        });
      });

      const rawContent = response?.content?.trim() || '';
      const decisionId = response?.decision_id || '';

      // Validate & Parse structured LLM output
      const validated = this.validateStructuredOutput(rawContent, input.decision, expectedAmount);
      if (!validated.success || !validated.data?.message) {
        this.logger.warn(
          `[AiGovernanceNegotiationClient] LLM output validation failed (${validated.error}). Discarding and using fallback.`,
        );
        return this.createFallbackResult(input, expectedAmount, validated.error, decisionId);
      }

      this.logger.log(
        `[AiGovernanceNegotiationClient] Successfully drafted speech via AiGovernance for shipment ${input.shipmentId} (decisionId: ${decisionId})`,
      );

      return {
        speech: validated.data.message,
        action: input.decision,
        amount: expectedAmount,
        currency: 'USD',
        aiDraftUsed: true,
        fallbackUsed: false,
        decisionId,
      };
    } catch (error: any) {
      this.logger.warn(
        `[AiGovernanceNegotiationClient] AiGovernance call failed for shipment ${input.shipmentId}: ${error.message}. Using safe deterministic fallback.`,
      );
      return this.createFallbackResult(input, expectedAmount, error.message);
    }
  }

  private validateStructuredOutput(
    rawText: string,
    expectedAction: NegotiationDecision,
    expectedAmount: number,
  ): { success: boolean; data?: StructuredLlmOutput; error?: string } {
    try {
      let cleanJson = rawText;
      if (cleanJson.startsWith('```')) {
        cleanJson = cleanJson.replace(/^```(?:json)?\n?/, '').replace(/\n?```$/, '').trim();
      }

      const parsed: StructuredLlmOutput = JSON.parse(cleanJson);

      // Guard 1: Action integrity
      if (!parsed.action || parsed.action.toUpperCase() !== expectedAction) {
        return {
          success: false,
          error: `Action mismatch: expected '${expectedAction}', received '${parsed.action}'`,
        };
      }

      // Guard 2: Currency integrity
      if (parsed.currency && parsed.currency.toUpperCase() !== 'USD') {
        return {
          success: false,
          error: `Currency mismatch: expected 'USD', received '${parsed.currency}'`,
        };
      }

      // Guard 3: Amount integrity
      if (parsed.amount !== undefined && Math.abs(parsed.amount - expectedAmount) > 0.05) {
        return {
          success: false,
          error: `Amount mismatch: expected $${expectedAmount}, received $${parsed.amount}`,
        };
      }

      if (!parsed.message || parsed.message.trim().length === 0) {
        return { success: false, error: 'Empty message in structured response' };
      }

      return { success: true, data: parsed };
    } catch (e: any) {
      return { success: false, error: `Invalid JSON output: ${e.message}` };
    }
  }

  private createFallbackResult(
    input: GenerateSpeechInput,
    amount: number,
    reason?: string,
    decisionId?: string,
  ): NegotiationSpeechResult {
    return {
      speech: this.getFallbackSpeech(input),
      action: input.decision,
      amount,
      currency: 'USD',
      aiDraftUsed: false,
      fallbackUsed: true,
      decisionId,
    };
  }

  public getFallbackSpeech(input: GenerateSpeechInput): string {
    switch (input.decision) {
      case 'ACCEPT':
        return `Cảm ơn quý khách! Chúng tôi xin chấp nhận mức giá $${input.offerPrice} cho lô hàng ${input.shipmentId}. Đơn hàng đã được xác nhận.`;
      case 'COUNTER_OFFER':
        return `Cảm ơn đề xuất $${input.offerPrice} của quý khách. Dựa trên chi phí vận chuyển thực tế, mức giá tốt nhất chúng tôi có thể đưa ra là $${input.counterOfferPrice}. Qúy khách có đồng ý không ạ?`;
      case 'HUMAN_HANDOFF':
        return `Đề xuất của quý khách cần được xem xét bởi chuyên viên tư vấn cao cấp. Chúng tôi đã chuyển cuộc hội thoại cho nhân viên hỗ trợ trực tiếp.`;
      case 'REJECT':
      default:
        return `Rất tiếc mức giá $${input.offerPrice} không thỏa mãn chi phí tối thiểu. Chúng tôi chưa thể thực hiện chuyến hàng này.`;
    }
  }
}
