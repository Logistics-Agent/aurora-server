import { Controller, Post, Body, Get, Param } from '@nestjs/common';
import { GrpcMethod } from '@nestjs/microservices';
import { NegotiationService, SubmitOfferInput } from '../../application/services/negotiation.service';

@Controller('negotiation')
export class NegotiationController {
  constructor(private readonly negotiationService: NegotiationService) {}

  @Post('offer')
  async submitOfferRest(@Body() body: SubmitOfferInput) {
    return this.negotiationService.submitOffer(body);
  }

  @Get('session/:id')
  async getSessionHistoryRest(@Param('id') id: string) {
    return this.negotiationService.getSessionHistory(id);
  }

  @GrpcMethod('NegotiationService', 'SubmitOffer')
  async submitOfferGrpc(data: SubmitOfferInput) {
    return this.negotiationService.submitOffer(data);
  }

  @GrpcMethod('NegotiationService', 'GetSessionHistory')
  async getSessionHistoryGrpc(data: { sessionId: string }) {
    return this.negotiationService.getSessionHistory(data.sessionId);
  }
}
