"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var GrpcExceptionFilter_1;
Object.defineProperty(exports, "__esModule", { value: true });
exports.GrpcExceptionFilter = void 0;
const common_1 = require("@nestjs/common");
const rxjs_1 = require("rxjs");
const microservices_1 = require("@nestjs/microservices");
const grpc_js_1 = require("@grpc/grpc-js");
let GrpcExceptionFilter = GrpcExceptionFilter_1 = class GrpcExceptionFilter {
    constructor() {
        this.logger = new common_1.Logger(GrpcExceptionFilter_1.name);
    }
    catch(exception, host) {
        this.logger.error(`Exception caught in gRPC filter: ${exception.message}`, exception.stack);
        let statusCode = grpc_js_1.status.INTERNAL;
        let message = exception.message || 'Internal server error';
        if (exception instanceof microservices_1.RpcException) {
            return (0, rxjs_1.throwError)(() => exception);
        }
        if (exception instanceof common_1.HttpException) {
            const httpStatus = exception.getStatus();
            switch (httpStatus) {
                case common_1.HttpStatus.BAD_REQUEST:
                    statusCode = grpc_js_1.status.INVALID_ARGUMENT;
                    break;
                case common_1.HttpStatus.NOT_FOUND:
                    statusCode = grpc_js_1.status.NOT_FOUND;
                    break;
                case common_1.HttpStatus.CONFLICT:
                    statusCode = grpc_js_1.status.ALREADY_EXISTS;
                    break;
                case common_1.HttpStatus.UNAUTHORIZED:
                case common_1.HttpStatus.FORBIDDEN:
                    statusCode = grpc_js_1.status.PERMISSION_DENIED;
                    break;
                default:
                    statusCode = grpc_js_1.status.INTERNAL;
            }
            message = exception.message;
        }
        return (0, rxjs_1.throwError)(() => new microservices_1.RpcException({ code: statusCode, message }));
    }
};
exports.GrpcExceptionFilter = GrpcExceptionFilter;
exports.GrpcExceptionFilter = GrpcExceptionFilter = GrpcExceptionFilter_1 = __decorate([
    (0, common_1.Catch)()
], GrpcExceptionFilter);
//# sourceMappingURL=grpc-exception.filter.js.map