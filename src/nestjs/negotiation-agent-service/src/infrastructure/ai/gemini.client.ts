import { Injectable, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { GoogleGenerativeAI } from '@google/generative-ai';

export interface GenerateSpeechInput {
  decision: 'ACCEPT' | 'COUNTER_OFFER' | 'HUMAN_HANDOFF' | 'REJECT';
  offerPrice: number;
  counterOfferPrice?: number;
  shipmentId: string;
  round: number;
}

@Injectable()
export class GeminiAIClient {
  private readonly logger = new Logger(GeminiAIClient.name);
  private genAI: GoogleGenerativeAI | null = null;

  constructor(private readonly configService: ConfigService) {
    const apiKey = this.configService.get<string>('GEMINI_API_KEY', '');
    if (apiKey && apiKey !== 'mock-gemini-api-key') {
      this.genAI = new GoogleGenerativeAI(apiKey);
    } else {
      this.logger.warn('[GeminiAIClient] GEMINI_API_KEY not set or is mock. Using deterministic speech template fallback.');
    }
  }

  /**
   * Generates natural language speech response for negotiation based on deterministic decision
   */
  async generateNegotiationSpeech(input: GenerateSpeechInput): Promise<string> {
    if (this.genAI) {
      try {
        const model = this.genAI.getGenerativeModel({ model: 'gemini-1.5-flash' });
        const prompt = `You are Aurora AI Logistics Agent. 
State: Shipment ${input.shipmentId}, Round ${input.round}.
Decision: ${input.decision}.
Customer Offer: $${input.offerPrice}.
Our Counter Price: ${input.counterOfferPrice ? '$' + input.counterOfferPrice : 'N/A'}.

Generate a concise, polite 2-sentence professional response in Vietnamese for the customer. Do NOT alter the prices or decision.`;

        const result = await Promise.race([
          model.generateContent(prompt),
          new Promise<never>((_, reject) =>
            setTimeout(() => reject(new Error('Gemini API Timeout (3.5s)')), 3500),
          ),
        ]);

        const text = result.response.text();
        if (text && text.trim().length > 0) {
          return text.trim();
        }
      } catch (err) {
        this.logger.warn(`[Gemini Fallback] API error (${err.message}). Using fallback template.`);
      }
    }

    // Deterministic Fallback Template
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
