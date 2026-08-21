"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.CloudEventFactory = void 0;
const crypto_1 = require("crypto");
class CloudEventFactory {
    static create(type, source, tenantId, correlationId, data) {
        return {
            specversion: '1.0',
            type,
            source,
            id: `evt_${(0, crypto_1.randomUUID)()}`,
            time: new Date().toISOString(),
            datacontenttype: 'application/json',
            tenant_id: tenantId || 'a0000000-0000-0000-0000-000000000001',
            correlation_id: correlationId || `corr_${(0, crypto_1.randomUUID)()}`,
            data,
        };
    }
}
exports.CloudEventFactory = CloudEventFactory;
//# sourceMappingURL=cloud-event.factory.js.map