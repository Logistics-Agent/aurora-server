import { plainToInstance } from 'class-transformer';
import { IsEnum, IsNumber, IsOptional, IsString, validateSync } from 'class-validator';

enum Environment {
  Development = 'development',
  Production = 'production',
  Test = 'test',
}

export class EnvironmentVariables {
  @IsEnum(Environment)
  @IsOptional()
  NODE_ENV: Environment = Environment.Development;

  @IsString()
  DATABASE_URL: string;

  @IsString()
  @IsOptional()
  DIRECT_URL?: string;

  @IsNumber()
  @IsOptional()
  GRPC_PORT: number = 5004;

  @IsString()
  @IsOptional()
  GRPC_HOST: string = '0.0.0.0';

  @IsString()
  @IsOptional()
  FINANCIAL_SERVICE_GRPC_URL: string = 'localhost:5003';

  @IsNumber()
  @IsOptional()
  DEFAULT_PAYMENT_TERMS_DAYS: number = 30;

  @IsString()
  @IsOptional()
  S3_BUCKET_NAME: string = 'aurora-private-docs';

  @IsString()
  @IsOptional()
  DEFAULT_CURRENCY: string = 'USD';
}

export function validate(config: Record<string, unknown>) {
  const validatedConfig = plainToInstance(EnvironmentVariables, config, {
    enableImplicitConversion: true,
  });

  const errors = validateSync(validatedConfig, {
    skipMissingProperties: false,
  });

  if (errors.length > 0) {
    throw new Error(`Config validation error: ${errors.toString()}`);
  }

  return validatedConfig;
}
