import { ExecutionContext } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { Metadata } from '@grpc/grpc-js';
import { of } from 'rxjs';
import { TenantInterceptor } from './tenant.interceptor';

describe('TenantInterceptor', () => {
  const config = { get: (key: string) => key === 'NODE_ENV' ? 'production' : 'shared-secret' };
  const interceptor = new TenantInterceptor(config as ConfigService);
  const next = { handle: () => of('ok') };

  function callWith(secret?: string) {
    const metadata = new Metadata();
    metadata.set('x-tenant-id', 'tenant-1');
    if (secret) metadata.set('x-internal-secret', secret);
    const data: Record<string, string> = {};
    const context = {
      switchToRpc: () => ({
        getContext: () => metadata,
        getData: () => data,
      }),
    } as ExecutionContext;
    return { context, data };
  }

  it('rejects a caller without the shared internal credential', () => {
    const { context } = callWith();
    expect(() => interceptor.intercept(context, next)).toThrow('Trusted service credentials');
  });

  it('accepts a trusted caller and sets the tenant from metadata', () => {
    const { context, data } = callWith('shared-secret');
    expect(() => interceptor.intercept(context, next)).not.toThrow();
    expect(data.tenantId).toBe('tenant-1');
  });
});
