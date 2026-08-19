"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const core_1 = require("@nestjs/core");
const microservices_1 = require("@nestjs/microservices");
const path_1 = require("path");
const fs_1 = require("fs");
const app_module_1 = require("./app.module");
const common_1 = require("@nestjs/common");
async function bootstrap() {
    const logger = new common_1.Logger('BillingServiceBootstrap');
    const grpcHost = process.env.GRPC_HOST || '0.0.0.0';
    const grpcPort = process.env.GRPC_PORT || '5004';
    const url = `${grpcHost}:${grpcPort}`;
    const possibleProtoPaths = [
        (0, path_1.join)(process.cwd(), '../../../protos/billing.proto'),
        (0, path_1.join)(process.cwd(), '../../protos/billing.proto'),
        (0, path_1.join)(__dirname, '../../../../protos/billing.proto'),
        (0, path_1.join)(__dirname, '../../../protos/billing.proto'),
    ];
    const protoPath = possibleProtoPaths.find((p) => (0, fs_1.existsSync)(p)) || possibleProtoPaths[0];
    logger.log(`Loading gRPC Proto from: ${protoPath}`);
    const app = await core_1.NestFactory.createMicroservice(app_module_1.AppModule, {
        transport: microservices_1.Transport.GRPC,
        options: {
            package: 'billing',
            protoPath: protoPath,
            url: url,
        },
    });
    await app.listen();
    logger.log(`Billing & Settlement NestJS gRPC Microservice is listening on ${url}`);
}
bootstrap();
//# sourceMappingURL=main.js.map