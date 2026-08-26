import { NestFactory } from '@nestjs/core';
import { MicroserviceOptions, Transport } from '@nestjs/microservices';
import { join } from 'path';
import { existsSync } from 'fs';
import { AppModule } from './app.module';
import { Logger, ValidationPipe } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';

async function bootstrap() {
  const logger = new Logger('NegotiationServiceBootstrap');
  const app = await NestFactory.create(AppModule);

  const configService = app.get(ConfigService);
  const httpPort = configService.get<number>('HTTP_PORT', 3006);
  const grpcHost = configService.get<string>('GRPC_HOST', '0.0.0.0');
  const grpcPort = configService.get<number>('PORT', 5006);
  const grpcUrl = `${grpcHost}:${grpcPort}`;

  const possibleProtoPaths = [
    join(process.cwd(), '../../../protos/negotiation.proto'),
    join(process.cwd(), '../../protos/negotiation.proto'),
    join(process.cwd(), '../protos/negotiation.proto'),
    join(process.cwd(), 'protos/negotiation.proto'),
    join(__dirname, '../../../../protos/negotiation.proto'),
    join(__dirname, '../../../protos/negotiation.proto'),
  ];

  const protoPath = possibleProtoPaths.find((p) => existsSync(p)) || possibleProtoPaths[0];

  logger.log(`Loading Negotiation gRPC Proto from: ${protoPath}`);

  // Connect gRPC Microservice
  app.connectMicroservice<MicroserviceOptions>({
    transport: Transport.GRPC,
    options: {
      package: 'negotiation',
      protoPath,
      url: grpcUrl,
    },
  });

  app.useGlobalPipes(
    new ValidationPipe({
      whitelist: true,
      transform: true,
    }),
  );

  await app.startAllMicroservices();
  logger.log(`🤖 Negotiation Agent gRPC Microservice listening on ${grpcUrl}`);

  await app.listen(httpPort);
  logger.log(`🤖 Negotiation Agent HTTP REST API running on port ${httpPort}`);
}

bootstrap();
