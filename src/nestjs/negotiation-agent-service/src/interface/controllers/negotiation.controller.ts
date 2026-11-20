import { Controller, UseFilters, UseInterceptors } from '@nestjs/common';
import { GrpcMethod } from '@nestjs/microservices';
import { NegotiationService, SubmitOfferInput } from '../../application/services/negotiation.service';
import { TenantInterceptor } from '../../infrastructure/security/tenant.interceptor';
import { GrpcExceptionFilter } from '../../infrastructure/filters/grpc-exception.filter';

@Controller()
@UseInterceptors(TenantInterceptor)
@UseFilters(GrpcExceptionFilter)
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

  @GrpcMethod('NegotiationService', 'ListSessions')
  async listSessionsGrpc(data: {
    tenantId?: string;
    status?: string;
    page?: number;
    pageSize?: number;
    page_size?: number;
    sourceThreadId?: string;
    source_thread_id?: string;
    shipmentId?: string;
    shipment_id?: string;
    quoteId?: string;
    quote_id?: string;
  }) {
    return this.negotiationService.listSessions({
      tenantId: data.tenantId,
      status: data.status,
      page: data.page,
      pageSize: data.pageSize ?? data.page_size,
      sourceThreadId: data.sourceThreadId || data.source_thread_id,
      shipmentId: data.shipmentId || data.shipment_id,
      quoteId: data.quoteId || data.quote_id,
    });
  }

  @GrpcMethod('NegotiationService', 'LinkSessionToShipment')
  async linkSessionToShipmentGrpc(data: {
    tenantId?: string;
    sessionId?: string;
    session_id?: string;
    shipmentId?: string;
    shipment_id?: string;
  }) {
    return this.negotiationService.linkSessionToShipment({
      tenantId: data.tenantId,
      sessionId: data.sessionId || data.session_id || '',
      shipmentId: data.shipmentId || data.shipment_id || '',
    });
  }
}
