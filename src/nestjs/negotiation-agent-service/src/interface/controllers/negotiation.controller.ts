import { Controller, UseInterceptors } from '@nestjs/common';
import { GrpcMethod } from '@nestjs/microservices';
import { NegotiationService, SubmitOfferInput } from '../../application/services/negotiation.service';
import { TenantInterceptor } from '../../infrastructure/security/tenant.interceptor';

@Controller()
@UseInterceptors(TenantInterceptor)
export class NegotiationController {
  constructor(private readonly negotiationService: NegotiationService) {}

  @GrpcMethod('NegotiationService', 'SubmitOffer')
  async submitOfferGrpc(data: SubmitOfferInput) {
    return this.negotiationService.submitOffer(data);
  }

  @GrpcMethod('NegotiationService', 'GetSessionHistory')
  async getSessionHistoryGrpc(data: { tenantId?: string; sessionId?: string; session_id?: string }) {
    const sessionId = data.sessionId || data.session_id || '';
    return this.negotiationService.getSessionHistory(sessionId, data.tenantId);
  }

  @GrpcMethod('NegotiationService', 'GetDraftSuggestion')
  async getDraftSuggestionGrpc(data: { tenantId?: string; negotiationSessionId?: string; negotiation_session_id?: string }) {
    const sessionId = data.negotiationSessionId || data.negotiation_session_id || '';
    return this.negotiationService.getDraftSuggestion(sessionId, data.tenantId);
  }
}
