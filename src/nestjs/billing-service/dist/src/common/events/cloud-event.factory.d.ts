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
export declare class CloudEventFactory {
    static create<T>(type: string, source: string, tenantId: string, correlationId: string | undefined, data: T): CloudEvent<T>;
}
