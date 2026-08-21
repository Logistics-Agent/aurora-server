import { Module } from '@nestjs/common';
import { BillingController } from '../interface/controllers/billing.controller';
import { BillingService } from '../application/services/billing.service';
import { InvoiceDomainService } from '../domain/services/invoice.domain-service';
import { GenerateInvoiceUseCase } from '../application/use-cases/generate-invoice.use-case';
import { StorageService } from '../infrastructure/storage/storage.service';
import { RabbitMQMessagingService } from '../infrastructure/messaging/rabbitmq.service';
import { ShipmentCompletedEventHandler } from '../infrastructure/messaging/event-handlers/shipment-completed.handler';
import { FinancialGrpcClient } from '../infrastructure/grpc-clients/financial.grpc-client';
import { OverdueInvoiceCronJob } from '../infrastructure/jobs/overdue-invoice.cron';
import { VNPTEInvoiceAdapter } from '../infrastructure/einvoice/einvoice.adapter';
import { DLQService } from '../infrastructure/messaging/dlq.service';

@Module({
  controllers: [BillingController],
  providers: [
    BillingService,
    InvoiceDomainService,
    GenerateInvoiceUseCase,
    StorageService,
    RabbitMQMessagingService,
    ShipmentCompletedEventHandler,
    FinancialGrpcClient,
    OverdueInvoiceCronJob,
    VNPTEInvoiceAdapter,
    DLQService,
  ],
  exports: [BillingService, GenerateInvoiceUseCase, VNPTEInvoiceAdapter, DLQService],
})
export class BillingModule {}
