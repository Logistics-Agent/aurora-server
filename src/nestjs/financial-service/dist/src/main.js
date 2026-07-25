"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const core_1 = require("@nestjs/core");
const microservices_1 = require("@nestjs/microservices");
const path_1 = require("path");
const app_module_1 = require("./app.module");
const common_1 = require("@nestjs/common");
async function bootstrap() {
    const logger = new common_1.Logger('FinancialServiceBootstrap');
    const grpcHost = process.env.GRPC_HOST || '0.0.0.0';
    const grpcPort = process.env.GRPC_PORT || '5003';
    const url = `${grpcHost}:${grpcPort}`;
    const protoPath = (0, path_1.join)(process.cwd(), '../../protos/financial.proto');
    logger.log(`Loading gRPC Proto from: ${protoPath}`);
    const app = await core_1.NestFactory.createMicroservice(app_module_1.AppModule, {
        transport: microservices_1.Transport.GRPC,
        options: {
            package: 'financial',
            protoPath: protoPath,
            url: url,
        },
    });
    await app.listen();
    logger.log(`Financial & Cost Estimation NestJS gRPC Microservice is listening on ${url}`);
}
bootstrap();
//# sourceMappingURL=main.js.map