"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var __metadata = (this && this.__metadata) || function (k, v) {
    if (typeof Reflect === "object" && typeof Reflect.metadata === "function") return Reflect.metadata(k, v);
};
var IdempotencyInterceptor_1;
Object.defineProperty(exports, "__esModule", { value: true });
exports.IdempotencyInterceptor = void 0;
const common_1 = require("@nestjs/common");
const rxjs_1 = require("rxjs");
const operators_1 = require("rxjs/operators");
const config_1 = require("@nestjs/config");
const ioredis_1 = require("ioredis");
let IdempotencyInterceptor = IdempotencyInterceptor_1 = class IdempotencyInterceptor {
    constructor(configService) {
        this.configService = configService;
        this.logger = new common_1.Logger(IdempotencyInterceptor_1.name);
        this.redisClient = null;
        this.inMemoryCache = new Map();
        const host = this.configService.get('redis.host', 'localhost');
        const port = this.configService.get('redis.port', 6379);
        try {
            this.redisClient = new ioredis_1.default({
                host,
                port,
                maxRetriesPerRequest: 1,
                lazyConnect: true,
            });
            this.redisClient.connect().catch(() => {
                this.redisClient = null;
            });
        }
        catch {
            this.redisClient = null;
        }
    }
    async intercept(context, next) {
        let idempotencyKey = null;
        let tenantId = 'a0000000-0000-0000-0000-000000000001';
        const type = context.getType();
        if (type === 'rpc') {
            const grpcContext = context.switchToRpc();
            const metadata = grpcContext.getContext();
            const data = grpcContext.getData();
            if (data && data.tenantId) {
                tenantId = data.tenantId;
            }
            if (metadata && typeof metadata.get === 'function') {
                const keyHeader = metadata.get('x-idempotency-key');
                if (keyHeader && keyHeader.length > 0) {
                    idempotencyKey = String(keyHeader[0]);
                }
            }
            if (!idempotencyKey && data && data.idempotencyKey) {
                idempotencyKey = String(data.idempotencyKey);
            }
        }
        else if (type === 'http') {
            const req = context.switchToHttp().getRequest();
            idempotencyKey = req.headers['x-idempotency-key'] || req.headers['idempotency-key'];
            tenantId = req.headers['x-tenant-id'] || tenantId;
        }
        if (!idempotencyKey) {
            return next.handle();
        }
        const redisKey = `idempotency:${tenantId}:${idempotencyKey}`;
        if (this.redisClient) {
            try {
                const cached = await this.redisClient.get(redisKey);
                if (cached) {
                    this.logger.log(`[Idempotency HIT - Redis] Key '${redisKey}' -> Returning cached response.`);
                    return (0, rxjs_1.of)(JSON.parse(cached));
                }
            }
            catch (err) {
                this.logger.warn(`[Idempotency] Redis get failed (${err.message}). Checking in-memory.`);
            }
        }
        const localCached = this.inMemoryCache.get(redisKey);
        if (localCached && localCached.expiresAt > Date.now()) {
            this.logger.log(`[Idempotency HIT - Memory] Key '${redisKey}' -> Returning cached response.`);
            return (0, rxjs_1.of)(localCached.data);
        }
        return next.handle().pipe((0, operators_1.tap)(async (response) => {
            const ttlSeconds = 120;
            if (this.redisClient) {
                try {
                    await this.redisClient.set(redisKey, JSON.stringify(response), 'EX', ttlSeconds);
                    this.logger.log(`[Idempotency SET - Redis] Cached key '${redisKey}' for ${ttlSeconds}s`);
                    return;
                }
                catch {
                }
            }
            this.inMemoryCache.set(redisKey, {
                data: response,
                expiresAt: Date.now() + ttlSeconds * 1000,
            });
            this.logger.log(`[Idempotency SET - Memory] Cached key '${redisKey}' for ${ttlSeconds}s`);
        }));
    }
};
exports.IdempotencyInterceptor = IdempotencyInterceptor;
exports.IdempotencyInterceptor = IdempotencyInterceptor = IdempotencyInterceptor_1 = __decorate([
    (0, common_1.Injectable)(),
    __metadata("design:paramtypes", [config_1.ConfigService])
], IdempotencyInterceptor);
//# sourceMappingURL=idempotency.interceptor.js.map