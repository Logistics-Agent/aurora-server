import { ExecutionContext, ForbiddenException, UnauthorizedException } from '@nestjs/common';
import { AuthGuard } from './auth.guard';
import { ActorType } from '../../domain/enums/actor-type.enum';
import { AuthIdentityMismatchException } from '../../domain/errors/assistant.errors';

describe('AuthGuard Trusted Identity Boundary', () => {
  let guard: AuthGuard;
  let mockConfigService: any;

  beforeEach(() => {
    mockConfigService = {
      get: jest.fn().mockImplementation((key: string) => {
        if (key === 'NODE_ENV') return 'production';
        if (key === 'INTERNAL_SERVICE_SECRET') return 'secret-bff-123';
        return undefined;
      }),
    };
    guard = new AuthGuard(mockConfigService);
  });

  const createMockContext = (headers: Record<string, string>): ExecutionContext => {
    const request: any = { headers, user: null };
    return {
      switchToHttp: () => ({
        getRequest: () => request,
      }),
    } as any;
  };

  it('should accept valid trusted BFF request with service-id and internal-secret', () => {
    const context = createMockContext({
      'x-service-id': 'Staff.Bff',
      'x-internal-secret': 'secret-bff-123',
      'x-tenant-id': 'tenant-bff-1',
      'x-user-id': 'user-staff-1',
      'x-actor-type': 'STAFF',
    });

    const canActivate = guard.canActivate(context);
    expect(canActivate).toBe(true);

    const req = context.switchToHttp().getRequest();
    expect(req.user.tenantId).toBe('tenant-bff-1');
    expect(req.user.userId).toBe('user-staff-1');
    expect(req.user.actorType).toBe(ActorType.STAFF);
  });

  it('should reject internal BFF request with invalid secret in production', () => {
    const context = createMockContext({
      'x-service-id': 'Staff.Bff',
      'x-internal-secret': 'wrong-secret',
      'x-tenant-id': 'tenant-1',
      'x-user-id': 'user-1',
    });

    expect(() => guard.canActivate(context)).toThrow(ForbiddenException);
  });

  it('should reject external request when JWT tenant does not match header tenant', () => {
    // Create JWT with tenant-A
    const payload = Buffer.from(JSON.stringify({ sub: 'user-1', tenant_id: 'tenant-A' })).toString('base64');
    const jwt = `header.${payload}.signature`;

    const context = createMockContext({
      'authorization': `Bearer ${jwt}`,
      'x-service-id': 'Staff.Bff',
      'x-internal-secret': 'secret-bff-123',
      'x-tenant-id': 'tenant-B', // mismatch
      'x-user-id': 'user-1',
    });

    expect(() => guard.canActivate(context)).toThrow(AuthIdentityMismatchException);
  });
});
