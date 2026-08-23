import {
  Controller,
  Post,
  Get,
  Param,
  Body,
  UseGuards,
  HttpCode,
  HttpStatus,
} from '@nestjs/common';
import { AuthGuard } from '../../infrastructure/security/auth.guard';
import { UserContext } from '../../infrastructure/security/current-user.decorator';
import { CurrentUser } from '../../infrastructure/security/current-user.interface';
import { ConversationalAssistantOrchestrator } from '../../application/orchestrator/conversational-assistant.orchestrator';
import { CreateConversationDto } from '../dto/create-conversation.dto';
import { SendMessageDto } from '../dto/send-message.dto';

@Controller('api/chat')
@UseGuards(AuthGuard)
export class ChatController {
  constructor(private readonly orchestrator: ConversationalAssistantOrchestrator) {}

  @Post('conversations')
  @HttpCode(HttpStatus.CREATED)
  async createConversation(
    @UserContext() user: CurrentUser,
    @Body() dto: CreateConversationDto,
  ) {
    const conv = await this.orchestrator.createConversation(
      user,
      dto?.preferredLanguage || 'vi',
    );

    return {
      conversationId: conv.id,
      status: conv.status,
      preferredLanguage: conv.preferredLanguage,
      createdAt: conv.createdAt.toISOString(),
    };
  }

  @Get('conversations')
  async listConversations(@UserContext() user: CurrentUser) {
    const list = await this.orchestrator.listConversations(user);
    return {
      conversations: list.map((c) => ({
        conversationId: c.id,
        status: c.status,
        preferredLanguage: c.preferredLanguage,
        createdAt: c.createdAt.toISOString(),
        lastActivityAt: c.lastActivityAt.toISOString(),
      })),
    };
  }

  @Get('conversations/:id')
  async getConversation(
    @Param('id') conversationId: string,
    @UserContext() user: CurrentUser,
  ) {
    const { conversation, messages } = await this.orchestrator.getConversation(
      conversationId,
      user,
    );

    return {
      conversation: {
        conversationId: conversation.id,
        status: conversation.status,
        preferredLanguage: conversation.preferredLanguage,
        createdAt: conversation.createdAt.toISOString(),
        lastActivityAt: conversation.lastActivityAt.toISOString(),
      },
      messages: messages.map((m) => ({
        messageId: m.id,
        role: m.role,
        content: m.content,
        intent: m.intent,
        sources: m.sources,
        conflicts: m.conflicts,
        insufficientEvidence: m.insufficientEvidence,
        createdAt: m.createdAt.toISOString(),
      })),
    };
  }

  @Post('conversations/:id/messages')
  @HttpCode(HttpStatus.OK)
  async sendMessage(
    @Param('id') conversationId: string,
    @UserContext() user: CurrentUser,
    @Body() dto: SendMessageDto,
  ) {
    return this.orchestrator.processMessage(conversationId, dto.message, user);
  }
}
