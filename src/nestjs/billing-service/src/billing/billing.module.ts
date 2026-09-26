import { Module } from '@nestjs/common';
import { BillingController } from '../interface/controllers/billing.controller';
import { BillingService } from '../application/services/billing.service';
import { InvoiceDomainService } from '../domain/services/invoice.domain-service';
import { GenerateInvoiceUseCase } from '../application/use-cases/generate-invoice.use-case';
import { RabbitMQMessagingService } from '../infrastructure/messaging/rabbitmq.service';
import { ShipmentCompletedEventHandler } from '../infrastructure/messaging/event-handlers/shipment-completed.handler';
import { ShipmentCompletedConsumer } from '../infrastructure/messaging/shipment-completed.consumer';
import { FinancialGrpcClient } from '../infrastructure/grpc-clients/financial.grpc-client';
import { OverdueInvoiceCronJob } from '../infrastructure/jobs/overdue-invoice.cron';

@Module({
  controllers: [BillingController],
  providers: [
    BillingService,
    InvoiceDomainService,
    GenerateInvoiceUseCase,
    RabbitMQMessagingService,
    ShipmentCompletedEventHandler,
    ShipmentCompletedConsumer,
    FinancialGrpcClient,
    OverdueInvoiceCronJob,
  ],
  exports: [BillingService, GenerateInvoiceUseCase],
})
export class BillingModule {}
