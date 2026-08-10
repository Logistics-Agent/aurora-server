import { Injectable, Logger } from '@nestjs/common';
import { Cron } from '@nestjs/schedule';
import { PrismaService } from '../prisma/prisma.service';

/**
 * TASK-002: Exchange Rate Sync Cron Job
 *
 * Chạy lúc 00:05 UTC mỗi ngày, đồng bộ tỷ giá USD/VND/EUR.
 * Phase 1: Dùng Mock rates (hardcoded nhưng lưu DB).
 * Phase 2: Tích hợp Vietcombank Open API thật.
 *
 * Redis caching key: financial:fx:{tenant_id}:{from}_{to}
 * TTL: 24 giờ
 */
@Injectable()
export class ExchangeRateSyncCronJob {
  private readonly logger = new Logger(ExchangeRateSyncCronJob.name);

  // Mock rates Phase 1 — Thay bằng API call thật ở Phase 2
  private readonly MOCK_RATES: Array<{
    fromCurrency: string;
    toCurrency: string;
    rate: number;
  }> = [
    { fromCurrency: 'USD', toCurrency: 'VND', rate: 25450.0 },
    { fromCurrency: 'EUR', toCurrency: 'VND', rate: 27800.0 },
    { fromCurrency: 'USD', toCurrency: 'EUR', rate: 0.915 },
    { fromCurrency: 'EUR', toCurrency: 'USD', rate: 1.093 },
    { fromCurrency: 'VND', toCurrency: 'USD', rate: 0.0000393 },
  ];

  constructor(private readonly prisma: PrismaService) {}

  @Cron('5 0 * * *') // Every day at 00:05 UTC
  async syncExchangeRates(): Promise<void> {
    const today = new Date();
    today.setUTCHours(0, 0, 0, 0); // Normalize to start of day UTC

    this.logger.log(`[CRON] Syncing exchange rates for ${today.toISOString()}...`);

    try {
      // Lấy tất cả tenant IDs đang active (hiện tại dùng mock)
      // Phase 2: Query từ IAM/Tenant Service
      const tenantIds = ['a0000000-0000-0000-0000-000000000001'];

      let syncCount = 0;
      for (const tenantId of tenantIds) {
        for (const rateConfig of this.MOCK_RATES) {
          await this.prisma.exchangeRate.upsert({
            where: {
              tenantId_fromCurrency_toCurrency_validDate: {
                tenantId,
                fromCurrency: rateConfig.fromCurrency,
                toCurrency: rateConfig.toCurrency,
                validDate: today,
              },
            },
            update: {
              rate: rateConfig.rate,
              source: 'MOCK',
            },
            create: {
              tenantId,
              fromCurrency: rateConfig.fromCurrency,
              toCurrency: rateConfig.toCurrency,
              rate: rateConfig.rate,
              validDate: today,
              source: 'MOCK',
            },
          });
          syncCount++;
        }
      }

      this.logger.log(`[CRON] Exchange rate sync complete: ${syncCount} rates upserted for ${tenantIds.length} tenant(s).`);
    } catch (error) {
      this.logger.error(`[CRON] Exchange rate sync failed: ${error.message}`);
    }
  }

  /**
   * Chạy thủ công khi service khởi động để warm data
   */
  async warmOnStartup(): Promise<void> {
    this.logger.log('[STARTUP] Warming exchange rates...');
    await this.syncExchangeRates();
  }
}
