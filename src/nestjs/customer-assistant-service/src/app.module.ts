import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { TerminusModule } from '@nestjs/terminus';

// Read Model & Persistence
import { ReadModelStore } from './read-model/read-model.store';
import { PostgresConversationRepository } from './infrastructure/persistence/postgres-conversation.repository';
import { RedisConversationCacheService } from './infrastructure/persistence/redis-conversation-cache.service';
import { CONVERSATION_REPOSITORY } from './domain/repositories/conversation.repository.interface';

// Security
import { AuthGuard } from './infrastructure/security/auth.guard';

// gRPC Clients
import { AiGovernanceGrpcClient } from './infrastructure/grpc/ai-governance.grpc-client';
import { RegulatoryComplianceGrpcClient } from './infrastructure/grpc/regulatory-compliance.grpc-client';

// Application Services & Tools
import { IntentRouterService } from './application/intent/intent-router.service';
import { ShipmentLookupTool } from './application/tools/shipment-lookup.tool';
import { BillingSummaryTool } from './application/tools/billing-summary.tool';
import { RegulatorySearchTool } from './application/tools/regulatory-search.tool';
import { KnowledgeSearchTool } from './application/tools/knowledge-search.tool';
import { ToolRegistryService } from './application/tools/tool-registry.service';
import { ConversationalPromptBuilder } from './application/prompt/conversational-prompt-builder';
import { ConversationalAssistantOrchestrator } from './application/orchestrator/conversational-assistant.orchestrator';
import { ConversationSummaryService } from './application/summary/conversation-summary.service';
import { AssistantCorpusAccessPolicy } from './application/policy/assistant-corpus-access.policy';

// Interface Controllers
import { ChatController } from './interface/controllers/chat.controller';
import { AssistantController } from './interface/controllers/assistant.controller';
import { HealthController } from './health/health.controller';

@Module({
  imports: [
    ConfigModule.forRoot({
      isGlobal: true,
    }),
    TerminusModule,
  ],
  controllers: [ChatController, AssistantController, HealthController],
  providers: [
    ReadModelStore,
    RedisConversationCacheService,
    {
      provide: CONVERSATION_REPOSITORY,
      useClass: PostgresConversationRepository,
    },
    AuthGuard,
    AiGovernanceGrpcClient,
    RegulatoryComplianceGrpcClient,
    IntentRouterService,
    AssistantCorpusAccessPolicy,
    ConversationSummaryService,
    ShipmentLookupTool,
    BillingSummaryTool,
    RegulatorySearchTool,
    KnowledgeSearchTool,
    ToolRegistryService,
    ConversationalPromptBuilder,
    ConversationalAssistantOrchestrator,
  ],
})
export class AppModule {}
