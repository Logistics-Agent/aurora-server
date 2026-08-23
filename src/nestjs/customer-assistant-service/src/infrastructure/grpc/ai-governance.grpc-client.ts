import { Injectable, OnModuleInit, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as grpc from '@grpc/grpc-js';
import * as protoLoader from '@grpc/proto-loader';
import * as path from 'path';
import { CurrentUser } from '../security/current-user.interface';

export interface AiGenerateResult {
  content: string;
  decisionId: string;
  automationLevel: string;
  requiresApproval: boolean;
  inputTokens: number;
  outputTokens: number;
}

@Injectable()
export class AiGovernanceGrpcClient implements OnModuleInit {
  private readonly logger = new Logger(AiGovernanceGrpcClient.name);
  private client: any;

  constructor(private readonly configService: ConfigService) {}

  onModuleInit() {
    const protoPath = path.resolve(__dirname, '../../../../../protos/ai_governance.proto');
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
      this.logger.log(`[AiGovernanceGrpcClient] Connected to AiGovernance at ${grpcUrl}`);
    } catch (err) {
      this.logger.warn(`[AiGovernanceGrpcClient] Failed to load proto from ${protoPath}. Initializing fallback client.`, err);
    }
  }

  async generate(
    capabilityCode: string,
    prompt: string,
    context: CurrentUser,
    maxOutputTokens = 2048,
  ): Promise<AiGenerateResult> {
    if (!this.client) {
      this.logger.warn('[AiGovernanceGrpcClient] Client not ready. Using deterministic fallback.');
      return {
        content: `[Fallback Response] Processed query under capability: ${capabilityCode}`,
        decisionId: 'dec-fallback',
        automationLevel: 'DETERMINISTIC_FALLBACK',
        requiresApproval: false,
        inputTokens: 10,
        outputTokens: 20,
      };
    }

    const metadata = new grpc.Metadata();
    metadata.add('x-service-id', 'customer-assistant-orchestrator');
    if (context.tenantId) metadata.add('x-tenant-id', context.tenantId);
    if (context.userId) metadata.add('x-user-id', context.userId);
    if (context.traceId) metadata.add('x-trace-id', context.traceId);

    const request = {
      capability_code: capabilityCode,
      prompt,
      max_output_tokens: maxOutputTokens,
      estimated_input_tokens: Math.max(10, Math.floor(prompt.length / 4)),
    };

    return new Promise((resolve, reject) => {
      this.client.Generate(request, metadata, { deadline: Date.now() + 45000 }, (err: any, response: any) => {
        if (err) {
          this.logger.error(`[AiGovernanceGrpcClient] Generate RPC error: ${err.message}`, err);
          return reject(err);
        }

        resolve({
          content: response.content || '',
          decisionId: response.decision_id || '',
          automationLevel: response.automation_level || 'ASSISTED',
          requiresApproval: response.requires_approval || false,
          inputTokens: Number(response.input_tokens || 0),
          outputTokens: Number(response.output_tokens || 0),
        });
      });
    });
  }
}
