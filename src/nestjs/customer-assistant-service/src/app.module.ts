import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { TerminusModule } from '@nestjs/terminus';
import { ReadModelStore } from './read-model/read-model.store';
import { CustomerAssistantService } from './application/services/assistant.service';
import { AssistantController } from './interface/controllers/assistant.controller';
import { HealthController } from './health/health.controller';

@Module({
  imports: [
    ConfigModule.forRoot({
      isGlobal: true,
    }),
    TerminusModule,
  ],
  controllers: [AssistantController, HealthController],
  providers: [ReadModelStore, CustomerAssistantService],
})
export class AppModule {}
