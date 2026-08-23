import { Injectable, ForbiddenException } from '@nestjs/common';
import { IAssistantTool, ToolExecutionContext, ToolResult } from './tool.interface';
import { AssistantIntent } from '../../domain/enums/assistant-intent.enum';
import { ActorType } from '../../domain/enums/actor-type.enum';
import { ReadModelStore } from '../../read-model/read-model.store';

@Injectable()
export class BillingSummaryTool implements IAssistantTool {
  readonly name = 'BillingSummaryTool';
  readonly description = 'Looks up customer balance, unpaid invoices, and payment summaries.';
  readonly supportedIntents = [AssistantIntent.BILLING_QUERY];
  readonly allowedActors = [ActorType.CUSTOMER, ActorType.STAFF, ActorType.ADMIN];

  constructor(private readonly readModel: ReadModelStore) {}

  async execute(context: ToolExecutionContext, params?: any): Promise<ToolResult> {
    const { actorType, customerId } = context.currentUser;

    if (!this.allowedActors.includes(actorType)) {
      throw new ForbiddenException(`Actor ${actorType} is not authorized to execute ${this.name}`);
    }

    const targetCustomerId =
      actorType === ActorType.CUSTOMER
        ? customerId || 'CUST-001'
        : params?.targetCustomerId || customerId || 'CUST-001';

    const summary = this.readModel.getCustomerBalanceSummary(targetCustomerId);

    return {
      toolName: this.name,
      success: true,
      data: summary,
      summary: `Tổng công nợ hiện tại của khách hàng ${targetCustomerId} là $${summary.totalDebt} (${summary.unpaidCount} hóa đơn chưa thanh toán hoàn tất). Hóa đơn gần nhất ${summary.invoices[0]?.invoiceNumber || 'N/A'} có số dư còn lại: $${summary.invoices[0]?.remainingBalance || 0}.`,
    };
  }
}
