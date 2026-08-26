import { Injectable, Logger } from '@nestjs/common';
import { AiGovernanceNegotiationClient, GenerateSpeechInput } from './ai-governance-negotiation.client';

/**
 * @deprecated Use AiGovernanceNegotiationClient directly.
 * Maintained as lightweight shim for backwards compatibility.
 */
@Injectable()
export class GeminiAIClient {
  private readonly logger = new Logger(GeminiAIClient.name);

  constructor(private readonly aiGovernanceClient?: AiGovernanceNegotiationClient) {}

  async generateNegotiationSpeech(input: GenerateSpeechInput): Promise<string> {
    if (this.aiGovernanceClient) {
      const result = await this.aiGovernanceClient.generateNegotiationSpeech(input);
      return result.speech;
    }
    return this.getFallbackSpeech(input);
  }

  private getFallbackSpeech(input: GenerateSpeechInput): string {
    switch (input.decision) {
      case 'ACCEPT':
        return `Cảm ơn quý khách! Chúng tôi xin chấp nhận mức giá $${input.offerPrice} cho lô hàng ${input.shipmentId}. Đơn hàng đã được xác nhận.`;
      case 'COUNTER_OFFER':
        return `Cảm ơn đề xuất $${input.offerPrice} của quý khách. Dựa trên chi phí vận chuyển thực tế, mức giá tốt nhất chúng tôi có thể đưa ra là $${input.counterOfferPrice}. Qúy khách có đồng ý không ạ?`;
      case 'HUMAN_HANDOFF':
        return `Đề xuất của quý khách cần được xem xét bởi chuyên viên tư vấn cao cấp. Chúng tôi đã chuyển cuộc hội thoại cho nhân viên hỗ trợ trực tiếp.`;
      case 'REJECT':
      default:
        return `Rất tiếc mức giá $${input.offerPrice} không thỏa mãn chi phí tối thiểu. Chúng tôi chưa thể thực hiện chuyến hàng này.`;
    }
  }
}
