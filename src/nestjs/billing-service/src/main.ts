import { NestFactory } from '@nestjs/core';
import { MicroserviceOptions, Transport } from '@nestjs/microservices';
import { join } from 'path';
import { existsSync } from 'fs';
import { AppModule } from './app.module';
import { Logger } from '@nestjs/common';

async function bootstrap() {
  const logger = new Logger('BillingServiceBootstrap');
  const grpcHost = process.env.GRPC_HOST || '0.0.0.0';
  const grpcPort = process.env.GRPC_PORT || '5004';
  const url = `${grpcHost}:${grpcPort}`;

  const possibleProtoPaths = [
    join(process.cwd(), '../../../protos/billing.proto'),
    join(process.cwd(), '../../protos/billing.proto'),
    join(__dirname, '../../../../protos/billing.proto'),
    join(__dirname, '../../../protos/billing.proto'),
  ];

  const protoPath = possibleProtoPaths.find((p) => existsSync(p)) || possibleProtoPaths[0];

  logger.log(`Loading gRPC Proto from: ${protoPath}`);

  const app = await NestFactory.createMicroservice<MicroserviceOptions>(AppModule, {
    transport: Transport.GRPC,
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
