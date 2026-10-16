import { Module } from '@nestjs/common';
import { BillingController } from '../interface/controllers/billing.controller';
import { BillingService } from '../application/services/billing.service';
import { CommercialAuthorityService } from '../application/services/commercial-authority.service';
import { InvoiceDomainService } from '../domain/services/invoice.domain-service';
import { GenerateInvoiceUseCase } from '../application/use-cases/generate-invoice.use-case';
import { RabbitMQMessagingService } from '../infrastructure/messaging/rabbitmq.service';
import { ShipmentWorkflowGrpcClient } from '../infrastructure/grpc-clients/shipment-workflow.grpc-client';
import { OverdueInvoiceCronJob } from '../infrastructure/jobs/overdue-invoice.cron';

@Module({
  controllers: [BillingController],
  providers: [
    BillingService,
    CommercialAuthorityService,
    InvoiceDomainService,
    GenerateInvoiceUseCase,
    RabbitMQMessagingService,
    ShipmentWorkflowGrpcClient,
    OverdueInvoiceCronJob,
  ],
  exports: [BillingService, GenerateInvoiceUseCase],
})
export class BillingModule {}
