import { Controller, Post, Body, Get, Param, UseGuards, ForbiddenException } from '@nestjs/common';
import { AuthGuard } from '../../infrastructure/security/auth.guard';
import { UserContext } from '../../infrastructure/security/current-user.decorator';
import { CurrentUser } from '../../infrastructure/security/current-user.interface';
import { ActorType } from '../../domain/enums/actor-type.enum';
import { ConversationalAssistantOrchestrator } from '../../application/orchestrator/conversational-assistant.orchestrator';

export interface LegacyChatInput {
  customerId?: string;
  message: string;
  conversationId?: string;
}

@Controller('chat')
@UseGuards(AuthGuard)
export class AssistantController {
  constructor(private readonly orchestrator: ConversationalAssistantOrchestrator) {}

  @Post()
  async chat(@UserContext() user: CurrentUser, @Body() body: LegacyChatInput) {
    const requestedCustomerId = body.customerId?.trim();
    if (
      requestedCustomerId &&
      requestedCustomerId !== user.customerId &&
      user.actorType !== ActorType.STAFF &&
      user.actorType !== ActorType.ADMIN
    ) {
      throw new ForbiddenException('Only staff can query another customer context.');
    }

    const effectiveUser: CurrentUser = requestedCustomerId
      ? { ...user, customerId: requestedCustomerId }
      : user;
    const convId = body.conversationId || `conv-legacy-${effectiveUser.userId.slice(0, 8)}`;
    const result = await this.orchestrator.processMessage(convId, body.message, effectiveUser);

    return {
      customerId: effectiveUser.customerId,
      intent: result.intent,
      replyMessage: result.answer,
      sources: result.sources,
      conflicts: result.conflicts,
      insufficientEvidence: result.insufficientEvidence,
      timestamp: result.timestamp,
    };
  }

  @Get('customer/:id')
  async getCustomerSummary(
    @Param('id') customerId: string,
    @UserContext() user: CurrentUser,
  ) {
    if (user.actorType !== ActorType.STAFF && user.actorType !== ActorType.ADMIN) {
      throw new ForbiddenException('Only staff can request another customer summary.');
    }
    const secureUser: CurrentUser = { ...user, customerId };
    const convId = `conv-summary-${secureUser.userId.slice(0, 8)}`;
    const result = await this.orchestrator.processMessage(
      convId,
      'công nợ và đơn hàng của tôi',
      secureUser,
    );

    return {
      customerId,
      intent: result.intent,
      replyMessage: result.answer,
      timestamp: result.timestamp,
    };
  }
}
