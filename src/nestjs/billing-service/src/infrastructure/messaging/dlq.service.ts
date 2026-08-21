import { Injectable, Logger } from '@nestjs/common';

export interface DeadLetterMessage {
  originalTopic: string;
  originalPayload: any;
  errorReason: string;
  retryCount: number;
  failedAt: string;
  tenantId: string;
}

@Injectable()
export class DLQService {
  private readonly logger = new Logger(DLQService.name);

  /**
   * Forward a failed message to the Dead Letter Queue after max retries exceeded
   */
  async sendToDeadLetterQueue(msg: DeadLetterMessage): Promise<void> {
    this.logger.error(
      `[DLQ FORWARD] Message on topic '${msg.originalTopic}' failed after ${msg.retryCount} retries. Reason: ${msg.errorReason} | Tenant: ${msg.tenantId}`,
    );

    // Phase 1: Structured logging to DLQ audit log
    // Phase 2: Publish directly to RabbitMQ 'logistics_events.dlq' exchange
    this.logger.warn(
      `[DLQ Logged] Exchange: 'logistics_events.dlq' | Payload: ${JSON.stringify(msg)}`,
    );
  }
}
