import { Injectable, Logger } from '@nestjs/common';
import { ActorType } from '../../domain/enums/actor-type.enum';

export interface CorpusAccessDecision {
  allowed: boolean;
  effectiveJurisdictions?: string[];
  effectiveCategories?: string[];
  reason?: string;
}

@Injectable()
export class AssistantCorpusAccessPolicy {
  private readonly logger = new Logger(AssistantCorpusAccessPolicy.name);

  // Categories strictly accessible to external Customers
  private readonly customerAllowedKnowledgeCategories = ['PUBLIC_FAQ', 'CUSTOMER_GUIDE', 'PUBLIC_PROCEDURE'];

  // Categories restricted to internal Staff / Admin
  private readonly internalRestrictedCategories = ['SOP', 'CARRIER_CONTRACT', 'INTERNAL_RULE', 'PRICING_POLICY'];

  canSearchRegulatory(
    actorType: ActorType,
    tenantId: string,
    requestedJurisdiction?: string,
  ): CorpusAccessDecision {
    switch (actorType) {
      case ActorType.CUSTOMER:
        // Customer can only access public platform/country regulations
        return {
          allowed: true,
          effectiveJurisdictions: requestedJurisdiction ? [requestedJurisdiction] : undefined,
        };

      case ActorType.STAFF:
      case ActorType.ADMIN:
        // Staff and Admin have full access to platform and tenant-specific regulations
        return {
          allowed: true,
          effectiveJurisdictions: requestedJurisdiction ? [requestedJurisdiction] : undefined,
        };

      case ActorType.SYSTEM:
        return {
          allowed: true,
          effectiveJurisdictions: requestedJurisdiction ? [requestedJurisdiction] : undefined,
        };

      default:
        return {
          allowed: false,
          reason: `Unknown actor type: ${actorType}`,
        };
    }
  }

  canSearchKnowledge(
    actorType: ActorType,
    tenantId: string,
    requestedCategories: string[] = [],
  ): CorpusAccessDecision {
    switch (actorType) {
      case ActorType.CUSTOMER: {
        // Enforce boundary: If customer explicitly asks for internal SOPs, DENY or sanitize
        const hasInternalOnly = requestedCategories.some((c) =>
          this.internalRestrictedCategories.includes(c.toUpperCase()),
        );

        if (hasInternalOnly) {
          this.logger.warn(`[CorpusAccessPolicy] Denied customer ${tenantId} access to internal knowledge categories: ${requestedCategories}`);
          return {
            allowed: false,
            reason: 'Quý khách không có quyền truy cập vào tài liệu SOP hoặc hợp đồng nội bộ của doanh nghiệp.',
          };
        }

        // Restrict search categories to public only
        const effective = requestedCategories.length > 0
          ? requestedCategories.filter((c) => this.customerAllowedKnowledgeCategories.includes(c.toUpperCase()))
          : this.customerAllowedKnowledgeCategories;

        return {
          allowed: true,
          effectiveCategories: effective,
        };
      }

      case ActorType.STAFF:
      case ActorType.ADMIN:
      case ActorType.SYSTEM:
        // Staff & Admin have full access to own-tenant SOPs and platform guidelines
        return {
          allowed: true,
          effectiveCategories: requestedCategories.length > 0 ? requestedCategories : undefined,
        };

      default:
        return {
          allowed: false,
          reason: `Unknown actor type: ${actorType}`,
        };
    }
  }
}
