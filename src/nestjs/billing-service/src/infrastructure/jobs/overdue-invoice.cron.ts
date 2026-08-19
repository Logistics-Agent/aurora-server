import { Injectable, Logger } from '@nestjs/common';
import { Cron } from '@nestjs/schedule';
import { PrismaService } from '../prisma/prisma.service';

/**
 * OverdueInvoiceCronJob
 *
 * Chạy lúc 00:05 sáng mỗi ngày để quét toàn bộ hóa đơn UNPAID/PARTIALLY_PAID
 * đã qua due_date và tự động cập nhật status sang OVERDUE.
 *
 * Lý do cần: CheckCustomerCredit xét điều kiện hóa đơn OVERDUE để chặn tín dụng mới.
 */
@Injectable()
export class OverdueInvoiceCronJob {
  private readonly logger = new Logger(OverdueInvoiceCronJob.name);

  constructor(private readonly prisma: PrismaService) {}

  @Cron('5 0 * * *') // Every day at 00:05 AM
  async markOverdueInvoices() {
    const now = new Date();
    this.logger.log(`[CRON] Running OverdueInvoice scan at ${now.toISOString()}...`);

    try {
      const result = await this.prisma.invoice.updateMany({
        where: {
          status: { in: ['UNPAID', 'PARTIALLY_PAID'] },
          dueDate: { lt: now },
        },
        data: {
          status: 'OVERDUE',
        },
      });

      this.logger.log(`[CRON] Marked ${result.count} invoice(s) as OVERDUE.`);
    } catch (error) {
      this.logger.error(`[CRON] Failed to mark overdue invoices: ${error.message}`);
    }
  }
}
