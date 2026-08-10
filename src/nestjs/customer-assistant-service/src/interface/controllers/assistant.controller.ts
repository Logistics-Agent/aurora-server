import { Controller, Post, Body, Get, Param } from '@nestjs/common';
import { CustomerAssistantService, CustomerChatInput } from '../../application/services/assistant.service';

@Controller('chat')
export class AssistantController {
  constructor(private readonly assistantService: CustomerAssistantService) {}

  @Post()
  async chat(@Body() body: CustomerChatInput) {
    return this.assistantService.processCustomerQuery(body);
  }

  @Get('customer/:id')
  async getCustomerSummary(@Param('id') customerId: string) {
    return this.assistantService.processCustomerQuery({
      customerId,
      message: 'công nợ và đơn hàng của tôi',
    });
  }
}
