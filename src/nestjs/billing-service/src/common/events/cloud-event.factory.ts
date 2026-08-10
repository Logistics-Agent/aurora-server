import { randomUUID } from 'crypto';

export interface CloudEvent<T = any> {
  specversion: string;
  type: string;
  source: string;
  id: string;
  time: string;
  datacontenttype: string;
  tenant_id: string;
  correlation_id: string;
  data: T;
}

export class CloudEventFactory {
  static create<T>(
    type: string,
    source: string,
    tenantId: string,
    correlationId: string | undefined,
    data: T,
  ): CloudEvent<T> {
    return {
      specversion: '1.0',
      type,
      source,
      id: `evt_${randomUUID()}`,
      time: new Date().toISOString(),
      datacontenttype: 'application/json',
      tenant_id: tenantId || 'a0000000-0000-0000-0000-000000000001',
      correlation_id: correlationId || `corr_${randomUUID()}`,
      data,
    };
  }
}
