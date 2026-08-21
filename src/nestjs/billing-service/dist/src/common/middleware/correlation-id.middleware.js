"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.CorrelationIdMiddleware = exports.CORRELATION_ID_HEADER = void 0;
const common_1 = require("@nestjs/common");
const crypto_1 = require("crypto");
exports.CORRELATION_ID_HEADER = 'x-correlation-id';
let CorrelationIdMiddleware = class CorrelationIdMiddleware {
    use(req, res, next) {
        const correlationId = req.headers[exports.CORRELATION_ID_HEADER] || `corr_${(0, crypto_1.randomUUID)()}`;
        req.headers[exports.CORRELATION_ID_HEADER] = correlationId;
        res.setHeader(exports.CORRELATION_ID_HEADER, correlationId);
        req.correlationId = correlationId;
        next();
    }
};
exports.CorrelationIdMiddleware = CorrelationIdMiddleware;
exports.CorrelationIdMiddleware = CorrelationIdMiddleware = __decorate([
    (0, common_1.Injectable)()
], CorrelationIdMiddleware);
//# sourceMappingURL=correlation-id.middleware.js.map