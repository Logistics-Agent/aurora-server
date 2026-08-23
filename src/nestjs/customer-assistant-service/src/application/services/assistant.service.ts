import { Injectable, Logger } from '@nestjs/common';
import { ReadModelStore } from '../../read-model/read-model.store';

export interface CustomerChatInput {
  tenantId?: string;
  customerId: string;
  message: string;
}

export interface CustomerChatResult {
  customerId: string;
  intent: 'TRACK_SHIPMENT' | 'CHECK_BALANCE' | 'GENERAL_HELP';
  replyMessage: string;
  readModelData?: any;
  timestamp: string;
}

@Injectable()
export class CustomerAssistantService {
  private readonly logger = new Logger(CustomerAssistantService.name);

  constructor(
    private readonly readModel: ReadModelStore,
  ) {}

  async processCustomerQuery(input: CustomerChatInput): Promise<CustomerChatResult> {

    const queryLower = input.message.toLowerCase();
    const customerId = input.customerId || 'CUST-001';

    let intent: 'TRACK_SHIPMENT' | 'CHECK_BALANCE' | 'GENERAL_HELP' = 'GENERAL_HELP';
    let replyMessage = '';
    let readModelData: any = null;

    // Rule-Based Intent Classifier (Deterministic fallback or RAG Function Calling)
    if (
      queryLower.includes('ở đâu') ||
      queryLower.includes('vị trí') ||
      queryLower.includes('theo dõi') ||
      queryLower.includes('shipment') ||
      queryLower.includes('đơn hàng')
    ) {
      intent = 'TRACK_SHIPMENT';
      const shipments = this.readModel.getShipmentsByCustomer(customerId);

      if (shipments.length > 0) {
        const primary = shipments[0];
        readModelData = primary;
        replyMessage = `Lô hàng ${primary.shipmentId} (${primary.originPort} ➔ ${primary.destinationPort}) hiện có trạng thái: **${primary.status}**. Vị trí hiện tại: ${primary.currentLocation || 'Đang di chuyển theo lịch trình'}.`;
      } else {
        replyMessage = `Hiện tại không tìm thấy lô hàng nào đang hoạt động cho khách hàng ${customerId}.`;
      }
    } else if (
      queryLower.includes('công nợ') ||
      queryLower.includes('hóa đơn') ||
      queryLower.includes('nợ') ||
      queryLower.includes('thanh toán') ||
      queryLower.includes('tài khoản')
    ) {
      intent = 'CHECK_BALANCE';
      const summary = this.readModel.getCustomerBalanceSummary(customerId);
      readModelData = summary;

      replyMessage = `Tổng công nợ hiện tại của quý khách là **$${summary.totalDebt}** (${summary.unpaidCount} hóa đơn chưa thanh toán hoàn tất). Hóa đơn mới nhất số ${summary.invoices[0]?.invoiceNumber || 'N/A'} có số dư còn lại: $${summary.invoices[0]?.remainingBalance || 0}.`;
    } else {
      intent = 'GENERAL_HELP';
      replyMessage = `Xin chào quý khách! Tôi là Trợ lý AI Aurora Logistics. Tôi có thể hỗ trợ quý khách **tra cứu vị trí đơn hàng**, **kiểm tra công nợ hóa đơn** hoặc **hướng dẫn thủ tục hải quan**. Quý khách cần hỗ trợ thông tin gì ạ?`;
    }

    this.logger.log(`[CustomerAssistant] Customer ${customerId} | Intent: ${intent} | Reply: "${replyMessage}"`);

    return {
      customerId,
      intent,
      replyMessage,
      readModelData,
      timestamp: new Date().toISOString(),
    };
  }
}
