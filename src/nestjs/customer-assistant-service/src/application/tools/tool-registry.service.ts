import { Injectable, Logger } from '@nestjs/common';
import { IAssistantTool, ToolExecutionContext, ToolResult } from './tool.interface';
import { AssistantIntent } from '../../domain/enums/assistant-intent.enum';
import { ShipmentLookupTool } from './shipment-lookup.tool';
import { BillingSummaryTool } from './billing-summary.tool';
import { RegulatorySearchTool } from './regulatory-search.tool';
import { KnowledgeSearchTool } from './knowledge-search.tool';

@Injectable()
export class ToolRegistryService {
  private readonly logger = new Logger(ToolRegistryService.name);
  private readonly tools = new Map<string, IAssistantTool>();

  constructor(
    private readonly shipmentLookupTool: ShipmentLookupTool,
    private readonly billingSummaryTool: BillingSummaryTool,
    private readonly regulatorySearchTool: RegulatorySearchTool,
    private readonly knowledgeSearchTool: KnowledgeSearchTool,
  ) {
    this.registerTool(this.shipmentLookupTool);
    this.registerTool(this.billingSummaryTool);
    this.registerTool(this.regulatorySearchTool);
    this.registerTool(this.knowledgeSearchTool);
  }

  registerTool(tool: IAssistantTool) {
    this.tools.set(tool.name, tool);
    this.logger.debug(`[ToolRegistry] Registered tool: ${tool.name}`);
  }

  getTool(name: string): IAssistantTool | undefined {
    return this.tools.get(name);
  }

  getToolsForIntent(intent: AssistantIntent): IAssistantTool[] {
    return Array.from(this.tools.values()).filter((tool) =>
      tool.supportedIntents.includes(intent),
    );
  }

  async executeTool(
    name: string,
    context: ToolExecutionContext,
    params?: any,
  ): Promise<ToolResult> {
    const tool = this.tools.get(name);
    if (!tool) {
      throw new Error(`Tool ${name} not found in registry.`);
    }

    this.logger.log(`[ToolRegistry] Executing tool ${name} for user ${context.currentUser.userId}`);
    return tool.execute(context, params);
  }
}
