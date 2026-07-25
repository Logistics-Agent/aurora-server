import { NestFactory } from '@nestjs/core';
import { MicroserviceOptions, Transport } from '@nestjs/microservices';
import { join } from 'path';
import { AppModule } from './app.module';
import { Logger } from '@nestjs/common';

async function bootstrap() {
  const logger = new Logger('FinancialServiceBootstrap');
  const grpcHost = process.env.GRPC_HOST || '0.0.0.0';
  const grpcPort = process.env.GRPC_PORT || '5003';
  const url = `${grpcHost}:${grpcPort}`;

  const protoPath = join(process.cwd(), '../../protos/financial.proto');

  logger.log(`Loading gRPC Proto from: ${protoPath}`);

  const app = await NestFactory.createMicroservice<MicroserviceOptions>(AppModule, {
    transport: Transport.GRPC,
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
