"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var DLQService_1;
Object.defineProperty(exports, "__esModule", { value: true });
exports.DLQService = void 0;
const common_1 = require("@nestjs/common");
let DLQService = DLQService_1 = class DLQService {
    constructor() {
        this.logger = new common_1.Logger(DLQService_1.name);
    }
    async sendToDeadLetterQueue(msg) {
        this.logger.error(`[DLQ FORWARD] Message on topic '${msg.originalTopic}' failed after ${msg.retryCount} retries. Reason: ${msg.errorReason} | Tenant: ${msg.tenantId}`);
        this.logger.warn(`[DLQ Logged] Exchange: 'logistics_events.dlq' | Payload: ${JSON.stringify(msg)}`);
    }
};
exports.DLQService = DLQService;
exports.DLQService = DLQService = DLQService_1 = __decorate([
    (0, common_1.Injectable)()
], DLQService);
//# sourceMappingURL=dlq.service.js.map