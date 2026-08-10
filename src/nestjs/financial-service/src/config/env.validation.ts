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

  @IsNumber()
  @IsOptional()
  GRPC_PORT: number = 5003;

  @IsString()
  @IsOptional()
  GRPC_HOST: string = '0.0.0.0';

  @IsNumber()
  @IsOptional()
  VOLUMETRIC_DIVISOR_AIR: number = 5000;

  @IsNumber()
  @IsOptional()
  VOLUMETRIC_DIVISOR_SEA: number = 6000;

  @IsNumber()
  @IsOptional()
  DEFAULT_VAT_RATE: number = 10.0;
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
