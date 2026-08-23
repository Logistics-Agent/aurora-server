import { Injectable, ForbiddenException } from '@nestjs/common';
import { IAssistantTool, ToolExecutionContext, ToolResult } from './tool.interface';
import { AssistantIntent } from '../../domain/enums/assistant-intent.enum';
import { ActorType } from '../../domain/enums/actor-type.enum';
import { RegulatoryComplianceGrpcClient } from '../../infrastructure/grpc/regulatory-compliance.grpc-client';
import { AssistantCorpusAccessPolicy } from '../policy/assistant-corpus-access.policy';

@Injectable()
export class RegulatorySearchTool implements IAssistantTool {
  readonly name = 'RegulatorySearchTool';
  readonly description = 'Searches authoritative laws, customs regulations, and decrees.';
  readonly supportedIntents = [AssistantIntent.REGULATORY_QUERY, AssistantIntent.HYBRID_RAG_QUERY];
  readonly allowedActors = [ActorType.CUSTOMER, ActorType.STAFF, ActorType.ADMIN, ActorType.SYSTEM];

  constructor(
    private readonly complianceClient: RegulatoryComplianceGrpcClient,
    private readonly accessPolicy: AssistantCorpusAccessPolicy,
  ) {}

  async execute(context: ToolExecutionContext, params?: any): Promise<ToolResult> {
    const { actorType, tenantId } = context.currentUser;
    const query = params?.query || '';
    const jurisdiction = params?.jurisdictionCode || '';

    // Enforce Corpus Access Policy (PATCH 9)
    const decision = this.accessPolicy.canSearchRegulatory(actorType, tenantId, jurisdiction);
    if (!decision.allowed) {
      throw new ForbiddenException(`Regulatory search denied: ${decision.reason}`);
    }

    const evidence = await this.complianceClient.queryRegulations(
      query,
      jurisdiction,
      params?.topK || 5,
      params?.minScore || 0.4,
      context.currentUser,
    );

    return {
      toolName: this.name,
      success: true,
      data: evidence,
      summary: `Found ${evidence.length} regulatory evidence items.`,
    };
  }
}
