export interface RealtimePayloadDto<T = any> {
  event: string;
  tenantId: string;
  timestamp: number;
  data: T;
}
