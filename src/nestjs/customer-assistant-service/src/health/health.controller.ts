import { Controller, Get } from '@nestjs/common';
import { HealthCheckService, HealthCheck } from '@nestjs/terminus';

@Controller('healthz')
export class HealthController {
  constructor(private readonly health: HealthCheckService) {}

  @Get('liveness')
  @HealthCheck()
  checkLiveness() {
    return {
      status: 'ok',
      service: 'customer-assistant-service',
      timestamp: new Date().toISOString(),
    };
  }

  @Get('readiness')
  @HealthCheck()
  checkReadiness() {
    return {
      status: 'ok',
      service: 'customer-assistant-service',
      readModel: 'ready',
      timestamp: new Date().toISOString(),
    };
  }
}
