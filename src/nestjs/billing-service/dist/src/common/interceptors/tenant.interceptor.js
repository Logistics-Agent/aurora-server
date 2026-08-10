"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.TenantInterceptor = void 0;
const common_1 = require("@nestjs/common");
let TenantInterceptor = class TenantInterceptor {
    intercept(context, next) {
        const type = context.getType();
        if (type === 'rpc') {
            const grpcContext = context.switchToRpc();
            const metadata = grpcContext.getContext();
            const data = grpcContext.getData();
            if (metadata && typeof metadata.get === 'function') {
                const tenantHeader = metadata.get('x-tenant-id');
                if (tenantHeader && tenantHeader.length > 0) {
                    data.tenantId = String(tenantHeader[0]);
                }
            }
        }
        return next.handle();
    }
};
exports.TenantInterceptor = TenantInterceptor;
exports.TenantInterceptor = TenantInterceptor = __decorate([
    (0, common_1.Injectable)()
], TenantInterceptor);
//# sourceMappingURL=tenant.interceptor.js.map