import { Injectable, OnModuleInit, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as grpc from '@grpc/grpc-js';
import * as protoLoader from '@grpc/proto-loader';
import * as path from 'path';

export interface AiDraftGenerateInput {
  action: 'ACCEPT' | 'COUNTER_OFFER' | 'HUMAN_HANDOFF' | 'REJECT';
  approvedAmount: number;
  currency: string;
  customerOffer: number;
  shipmentId: string;
  round: number;
  customerTier?: string;
  tenantId?: string;
  userId?: string;
  traceId?: string;
}

export interface AiDraftGenerateResult {
  content: string;
  decisionId: string;
  automationLevel: string;
  requiresApproval: boolean;
  inputTokens: number;
  outputTokens: number;
  isFallback: boolean;
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
    } catch (err) {
      this.logger.warn(
        `[AiGovernanceNegotiationClient] Failed to load proto from ${protoPath}. Initializing fallback client.`,
        err,
      );
    }
  }

  async generateNegotiationDraft(input: AiDraftGenerateInput): Promise<AiDraftGenerateResult> {
    const promptPayload = {
      action: input.action,
      approved_amount: input.approvedAmount,
      currency: input.currency,
      customer_offer: input.customerOffer,
      shipment_id: input.shipmentId,
      round: input.round,
      customer_tier: input.customerTier || 'STANDARD',
      business_reason: `Deterministic decision: ${input.action} at ${input.approvedAmount} ${input.currency}.`,
    };

    const prompt = JSON.stringify(promptPayload);

    if (!this.client) {
      this.logger.warn('[AiGovernanceNegotiationClient] Client not ready. Using deterministic fallback.');
      return {
        content: this.getDeterministicFallback(input),
        decisionId: 'dec-fallback',
        automationLevel: 'DETERMINISTIC_FALLBACK',
        requiresApproval: false,
        inputTokens: 10,
        outputTokens: 20,
        isFallback: true,
      };
    }

    const metadata = new grpc.Metadata();
    metadata.add('x-service-id', 'negotiation-agent-service');
    if (input.tenantId) metadata.add('x-tenant-id', input.tenantId);
    if (input.userId) metadata.add('x-user-id', input.userId);
    if (input.traceId) metadata.add('x-trace-id', input.traceId);

    const request = {
      capability_code: 'negotiation.draft',
      prompt,
      max_output_tokens: 1024,
      estimated_input_tokens: Math.max(10, Math.floor(prompt.length / 4)),
    };

    return new Promise((resolve) => {
      this.client.Generate(request, metadata, { deadline: Date.now() + 10000 }, (err: any, response: any) => {
        if (err) {
          this.logger.warn(`[AiGovernanceNegotiationClient] Generate RPC error: ${err.message}. Falling back.`);
          return resolve({
            content: this.getDeterministicFallback(input),
            decisionId: 'dec-fallback-err',
            automationLevel: 'DETERMINISTIC_FALLBACK',
            requiresApproval: false,
            inputTokens: 10,
            outputTokens: 20,
            isFallback: true,
          });
        }

        resolve({
          content: response.content || this.getDeterministicFallback(input),
          decisionId: response.decision_id || '',
          automationLevel: response.automation_level || 'ASSISTED',
          requiresApproval: response.requires_approval || false,
          inputTokens: Number(response.input_tokens || 0),
          outputTokens: Number(response.output_tokens || 0),
          isFallback: false,
        });
      });
    });
  }

  public getDeterministicFallback(input: AiDraftGenerateInput): string {
    switch (input.action) {
      case 'ACCEPT':
        return `Dear Customer,\n\nThank you for your offer of ${input.customerOffer} ${input.currency} for shipment ${input.shipmentId}. We are pleased to confirm that your offer has been accepted.\n\nBest regards,\nLogistics Team`;
      case 'COUNTER_OFFER':
        return `Dear Customer,\n\nThank you for your quotation request of ${input.customerOffer} ${input.currency} regarding shipment ${input.shipmentId}. Based on our current route and capacity calculations, our best possible counter-offer is ${input.approvedAmount} ${input.currency}.\n\nPlease let us know if you would like to proceed.\n\nBest regards,\nLogistics Team`;
      case 'HUMAN_HANDOFF':
        return `Dear Customer,\n\nThank you for your proposal regarding shipment ${input.shipmentId}. Your request has been escalated to a dedicated commercial account manager who will contact you shortly with a personalized solution.\n\nBest regards,\nLogistics Team`;
      case 'REJECT':
      default:
        return `Dear Customer,\n\nThank you for your inquiry regarding shipment ${input.shipmentId}. Unfortunately, we are unable to accept the offered rate of ${input.customerOffer} ${input.currency} as it is below our operational threshold.\n\nBest regards,\nLogistics Team`;
    }
  }
}
