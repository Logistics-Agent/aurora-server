import { AssistantIntent } from '../../domain/enums/assistant-intent.enum';
import { ActorType } from '../../domain/enums/actor-type.enum';
import { CurrentUser } from '../../infrastructure/security/current-user.interface';

export interface ToolExecutionContext {
  currentUser: CurrentUser;
  conversationId: string;
}

export interface ToolResult {
  toolName: string;
  success: boolean;
  data: any;
  summary: string;
}

export interface IAssistantTool {
  readonly name: string;
  readonly description: string;
  readonly supportedIntents: AssistantIntent[];
  readonly allowedActors: ActorType[];

  execute(context: ToolExecutionContext, params?: any): Promise<ToolResult>;
}
