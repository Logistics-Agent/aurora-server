import { HttpException, HttpStatus } from '@nestjs/common';

export enum AssistantErrorCode {
  CONVERSATION_NOT_FOUND = 'CONVERSATION_NOT_FOUND',
  CONVERSATION_ACCESS_DENIED = 'CONVERSATION_ACCESS_DENIED',
  CONVERSATION_ARCHIVED = 'CONVERSATION_ARCHIVED',
  CONVERSATION_CONCURRENCY_CONFLICT = 'CONVERSATION_CONCURRENCY_CONFLICT',
  TOOL_ACCESS_DENIED = 'TOOL_ACCESS_DENIED',
  GROUNDING_VALIDATION_FAILED = 'GROUNDING_VALIDATION_FAILED',
  SUMMARY_JOB_FAILED = 'SUMMARY_JOB_FAILED',
  AI_POLICY_DENIED = 'AI_POLICY_DENIED',
  AI_PROVIDER_UNAVAILABLE = 'AI_PROVIDER_UNAVAILABLE',
  AUTH_IDENTITY_MISMATCH = 'AUTH_IDENTITY_MISMATCH',
}

export class AssistantBaseException extends HttpException {
  constructor(
    public readonly errorCode: AssistantErrorCode,
    message: string,
    status: HttpStatus,
    public readonly details?: any,
  ) {
    super(
      {
        statusCode: status,
        errorCode,
        message,
        details,
        timestamp: new Date().toISOString(),
      },
      status,
    );
  }
}

export class ConversationNotFoundException extends AssistantBaseException {
  constructor(conversationId: string) {
    super(
      AssistantErrorCode.CONVERSATION_NOT_FOUND,
      `Conversation ${conversationId} was not found.`,
      HttpStatus.NOT_FOUND,
    );
  }
}

export class ConversationAccessDeniedException extends AssistantBaseException {
  constructor(conversationId: string) {
    super(
      AssistantErrorCode.CONVERSATION_ACCESS_DENIED,
      `Access to conversation ${conversationId} is denied.`,
      HttpStatus.FORBIDDEN,
    );
  }
}

export class ConversationArchivedException extends AssistantBaseException {
  constructor(conversationId: string) {
    super(
      AssistantErrorCode.CONVERSATION_ARCHIVED,
      `Conversation ${conversationId} is archived and cannot receive new messages.`,
      HttpStatus.BAD_REQUEST,
    );
  }
}

export class ConversationConcurrencyConflictException extends AssistantBaseException {
  constructor(conversationId: string, currentVersion: number, expectedVersion: number) {
    super(
      AssistantErrorCode.CONVERSATION_CONCURRENCY_CONFLICT,
      `Concurrency conflict on conversation ${conversationId}. Expected version ${expectedVersion}, but found version ${currentVersion}.`,
      HttpStatus.CONFLICT,
    );
  }
}

export class ToolAccessDeniedException extends AssistantBaseException {
  constructor(toolName: string, reason: string) {
    super(
      AssistantErrorCode.TOOL_ACCESS_DENIED,
      `Access to tool ${toolName} denied: ${reason}`,
      HttpStatus.FORBIDDEN,
    );
  }
}

export class GroundingValidationFailedException extends AssistantBaseException {
  constructor(reason: string, details?: any) {
    super(
      AssistantErrorCode.GROUNDING_VALIDATION_FAILED,
      `Grounded response validation failed: ${reason}`,
      HttpStatus.UNPROCESSABLE_ENTITY,
      details,
    );
  }
}

export class SummaryJobFailedException extends AssistantBaseException {
  constructor(conversationId: string, reason: string) {
    super(
      AssistantErrorCode.SUMMARY_JOB_FAILED,
      `Summary generation failed for conversation ${conversationId}: ${reason}`,
      HttpStatus.INTERNAL_SERVER_ERROR,
    );
  }
}

export class AiPolicyDeniedException extends AssistantBaseException {
  constructor(capabilityCode: string, reason: string) {
    super(
      AssistantErrorCode.AI_POLICY_DENIED,
      `AiGovernance policy denied capability ${capabilityCode}: ${reason}`,
      HttpStatus.FORBIDDEN,
    );
  }
}

export class AiProviderUnavailableException extends AssistantBaseException {
  constructor(reason: string) {
    super(
      AssistantErrorCode.AI_PROVIDER_UNAVAILABLE,
      `AI execution provider is temporarily unavailable: ${reason}`,
      HttpStatus.SERVICE_UNAVAILABLE,
    );
  }
}

export class AuthIdentityMismatchException extends AssistantBaseException {
  constructor(reason: string) {
    super(
      AssistantErrorCode.AUTH_IDENTITY_MISMATCH,
      `Authentication identity mismatch: ${reason}`,
      HttpStatus.UNAUTHORIZED,
    );
  }
}
