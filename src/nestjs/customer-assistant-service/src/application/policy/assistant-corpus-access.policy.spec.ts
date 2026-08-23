import { AssistantCorpusAccessPolicy } from './assistant-corpus-access.policy';
import { ActorType } from '../../domain/enums/actor-type.enum';
import { CurrentUser } from '../../infrastructure/security/current-user.interface';

describe('AssistantCorpusAccessPolicy', () => {
  let policy: AssistantCorpusAccessPolicy;

  beforeEach(() => {
    policy = new AssistantCorpusAccessPolicy();
  });

  describe('Regulatory Corpus Access', () => {
    it('should allow CUSTOMER to query public regulations', () => {
      const res = policy.canSearchRegulatory(ActorType.CUSTOMER, 'tenant-1', 'MY');
      expect(res.allowed).toBe(true);
      expect(res.effectiveJurisdictions).toEqual(['MY']);
    });

    it('should allow STAFF to query regulations', () => {
      const res = policy.canSearchRegulatory(ActorType.STAFF, 'tenant-1', 'VN');
      expect(res.allowed).toBe(true);
      expect(res.effectiveJurisdictions).toEqual(['VN']);
    });
  });

  describe('Knowledge Corpus Access', () => {
    it('should DENY CUSTOMER access to internal SOPs and contracts', () => {
      const res = policy.canSearchKnowledge(ActorType.CUSTOMER, 'tenant-1', ['SOP', 'CARRIER_CONTRACT']);
      expect(res.allowed).toBe(false);
      expect(res.reason).toContain('không có quyền truy cập');
    });

    it('should restrict CUSTOMER to public knowledge categories when requesting default', () => {
      const res = policy.canSearchKnowledge(ActorType.CUSTOMER, 'tenant-1', []);
      expect(res.allowed).toBe(true);
      expect(res.effectiveCategories).toEqual(['PUBLIC_FAQ', 'CUSTOMER_GUIDE', 'PUBLIC_PROCEDURE']);
    });

    it('should allow STAFF full access to company SOPs', () => {
      const res = policy.canSearchKnowledge(ActorType.STAFF, 'tenant-1', ['SOP', 'CARRIER_CONTRACT']);
      expect(res.allowed).toBe(true);
      expect(res.effectiveCategories).toEqual(['SOP', 'CARRIER_CONTRACT']);
    });

    it('should allow TENANT_ADMIN access to own tenant knowledge', () => {
      const tenantAdminUser: CurrentUser = {
        tenantId: 'tenant-1',
        userId: 'admin-1',
        actorType: ActorType.ADMIN,
        roles: ['TENANT_ADMIN'],
        permissions: [],
      };

      const res = policy.canSearchKnowledge(ActorType.ADMIN, 'tenant-1', ['INTERNAL_RULE'], tenantAdminUser);
      expect(res.allowed).toBe(true);
      expect(res.effectiveCategories).toEqual(['INTERNAL_RULE']);
    });

    it('should allow PLATFORM_ADMIN to search platform guidelines', () => {
      const platformAdminUser: CurrentUser = {
        tenantId: 'platform',
        userId: 'super-admin-1',
        actorType: ActorType.ADMIN,
        roles: ['PLATFORM_ADMIN'],
        permissions: ['platform.audit.tenant.read'],
      };

      const res = policy.canSearchKnowledge(ActorType.ADMIN, 'tenant-1', ['SOP'], platformAdminUser);
      expect(res.allowed).toBe(true);
    });
  });
});
