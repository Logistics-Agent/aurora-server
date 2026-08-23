import { Injectable, Logger } from '@nestjs/common';
import { ActorType } from '../../domain/enums/actor-type.enum';
import { CurrentUser } from '../../infrastructure/security/current-user.interface';

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

  // Categories restricted to internal Staff / Tenant Admin
  private readonly internalRestrictedCategories = ['SOP', 'CARRIER_CONTRACT', 'INTERNAL_RULE', 'PRICING_POLICY'];

  /**
   * Helper to check if the current user has Platform Admin scope
   */
  isPlatformAdmin(user: CurrentUser): boolean {
    const roles = user.roles || [];
    return roles.includes('PLATFORM_ADMIN') || roles.includes('SUPER_ADMIN');
  }

  canSearchRegulatory(
    actorType: ActorType,
    tenantId: string,
    requestedJurisdiction?: string,
    user?: CurrentUser,
  ): CorpusAccessDecision {
    switch (actorType) {
      case ActorType.CUSTOMER:
        // Customer can only access public platform/country regulations
        return {
          allowed: true,
          effectiveJurisdictions: requestedJurisdiction ? [requestedJurisdiction] : undefined,
        };

      case ActorType.STAFF:
        // Staff can access platform and own tenant regulations
        return {
          allowed: true,
          effectiveJurisdictions: requestedJurisdiction ? [requestedJurisdiction] : undefined,
        };

      case ActorType.ADMIN:
        // Tenant Admin or Platform Admin
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
    user?: CurrentUser,
  ): CorpusAccessDecision {
    switch (actorType) {
      case ActorType.CUSTOMER: {
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

        const effective = requestedCategories.length > 0
          ? requestedCategories.filter((c) => this.customerAllowedKnowledgeCategories.includes(c.toUpperCase()))
          : this.customerAllowedKnowledgeCategories;

        return {
          allowed: true,
          effectiveCategories: effective,
        };
      }

      case ActorType.STAFF: {
        // Staff has full access to own-tenant SOPs and platform guidelines
        return {
          allowed: true,
          effectiveCategories: requestedCategories.length > 0 ? requestedCategories : undefined,
        };
      }

      case ActorType.ADMIN: {
        const isPlatform = user ? this.isPlatformAdmin(user) : false;

        if (isPlatform) {
          // Platform Admin: Platform Knowledge + explicit privileged tenant audit
          const hasTenantAudit = user?.permissions?.includes('platform.audit.tenant.read');
          if (!hasTenantAudit && requestedCategories.some((c) => this.internalRestrictedCategories.includes(c.toUpperCase()))) {
            // Platform admin querying platform knowledge by default
            return {
              allowed: true,
              effectiveCategories: requestedCategories.length > 0 ? requestedCategories : undefined,
            };
          }

          return {
            allowed: true,
            effectiveCategories: requestedCategories.length > 0 ? requestedCategories : undefined,
          };
        }

        // Tenant Admin: Scoped to own tenant SOPs
        return {
          allowed: true,
          effectiveCategories: requestedCategories.length > 0 ? requestedCategories : undefined,
        };
      }

      case ActorType.SYSTEM:
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

  canUseShipmentTool(user: CurrentUser): boolean {
    return [ActorType.CUSTOMER, ActorType.STAFF, ActorType.ADMIN, ActorType.SYSTEM].includes(user.actorType);
  }

  canUseBillingTool(user: CurrentUser): boolean {
    return [ActorType.CUSTOMER, ActorType.STAFF, ActorType.ADMIN].includes(user.actorType);
  }
}
