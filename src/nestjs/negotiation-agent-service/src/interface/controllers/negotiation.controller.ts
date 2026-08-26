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
  async getSessionHistoryGrpc(data: { sessionId?: string; session_id?: string }) {
    const sessionId = data.sessionId || data.session_id || '';
    return this.negotiationService.getSessionHistory(sessionId);
  }

  @GrpcMethod('NegotiationService', 'GetDraftSuggestion')
  async getDraftSuggestionGrpc(data: { negotiationSessionId?: string; negotiation_session_id?: string }) {
    const sessionId = data.negotiationSessionId || data.negotiation_session_id || '';
    return this.negotiationService.getDraftSuggestion(sessionId);
  }
}
