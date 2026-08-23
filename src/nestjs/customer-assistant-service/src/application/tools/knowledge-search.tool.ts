import { Injectable, ForbiddenException } from '@nestjs/common';
import { IAssistantTool, ToolExecutionContext, ToolResult } from './tool.interface';
import { AssistantIntent } from '../../domain/enums/assistant-intent.enum';
import { ActorType } from '../../domain/enums/actor-type.enum';
import { RegulatoryComplianceGrpcClient } from '../../infrastructure/grpc/regulatory-compliance.grpc-client';
import { AssistantCorpusAccessPolicy } from '../policy/assistant-corpus-access.policy';

@Injectable()
export class KnowledgeSearchTool implements IAssistantTool {
  readonly name = 'KnowledgeSearchTool';
  readonly description = 'Searches internal company SOPs, carrier contracts, and guidelines.';
  readonly supportedIntents = [AssistantIntent.KNOWLEDGE_QUERY, AssistantIntent.HYBRID_RAG_QUERY];
  readonly allowedActors = [ActorType.CUSTOMER, ActorType.STAFF, ActorType.ADMIN, ActorType.SYSTEM];

  constructor(
    private readonly complianceClient: RegulatoryComplianceGrpcClient,
    private readonly accessPolicy: AssistantCorpusAccessPolicy,
  ) {}

  async execute(context: ToolExecutionContext, params?: any): Promise<ToolResult> {
    const { actorType, tenantId } = context.currentUser;
    const query = params?.query || '';
    const requestedCategories = params?.categories || [];

    // Enforce Corpus Access Policy (PATCH 9)
    const decision = this.accessPolicy.canSearchKnowledge(actorType, tenantId, requestedCategories);
    if (!decision.allowed) {
      throw new ForbiddenException(`Knowledge search denied: ${decision.reason}`);
    }

    const effectiveCategories = decision.effectiveCategories || requestedCategories;

    const evidence = await this.complianceClient.queryKnowledge(
      query,
      effectiveCategories,
      params?.topK || 5,
      params?.minScore || 0.4,
      context.currentUser,
    );

    return {
      toolName: this.name,
      success: true,
      data: evidence,
      summary: `Found ${evidence.length} company SOP knowledge items.`,
    };
  }
}
