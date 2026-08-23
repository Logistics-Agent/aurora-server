package com.aurora.aigovernance.governance.application.service;

import com.aurora.aigovernance.governance.domain.entity.UsageRecord;
import com.aurora.aigovernance.governance.domain.enums.QuotaMetric;
import com.aurora.aigovernance.governance.domain.enums.QuotaPeriod;
import com.aurora.aigovernance.governance.domain.valueobject.PeriodKey;
import com.aurora.aigovernance.governance.infrastructure.cache.QuotaRedisAdapter;
import com.aurora.aigovernance.governance.infrastructure.persistence.UsageRecordRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Instant;
import java.util.Optional;
import java.util.UUID;

/**
 * Service to record and sync tenant usage to both Redis (ephemeral fast-check)
 * and PostgreSQL (durable source of truth).
 */
@Service
public class QuotaSyncService {

    private static final Logger log = LoggerFactory.getLogger(QuotaSyncService.class);

    private final QuotaRedisAdapter quotaRedisAdapter;
    private final UsageRecordRepository usageRecordRepository;
    private final PeriodKeyCalculator periodKeyCalculator;

    public QuotaSyncService(
            QuotaRedisAdapter quotaRedisAdapter,
            UsageRecordRepository usageRecordRepository,
            PeriodKeyCalculator periodKeyCalculator) {
        this.quotaRedisAdapter = quotaRedisAdapter;
        this.usageRecordRepository = usageRecordRepository;
        this.periodKeyCalculator = periodKeyCalculator;
    }

    /**
     * Record execution usage for a tenant across all relevant periods (MINUTE, DAY, MONTH).
     */
    @Transactional
    public void recordUsage(UUID tenantId, long requestCount, long totalTokens, Instant timestamp) {
        // 1. Record requests
        if (requestCount > 0) {
            recordDimension(tenantId, QuotaMetric.REQUESTS, QuotaPeriod.MINUTE, requestCount, timestamp);
            recordDimension(tenantId, QuotaMetric.REQUESTS, QuotaPeriod.DAY, requestCount, timestamp);
        }

        // 2. Record tokens
        if (totalTokens > 0) {
            recordDimension(tenantId, QuotaMetric.TOKENS, QuotaPeriod.MINUTE, totalTokens, timestamp);
            recordDimension(tenantId, QuotaMetric.TOKENS, QuotaPeriod.MONTH, totalTokens, timestamp);
        }
    }

    private void recordDimension(UUID tenantId, QuotaMetric metric, QuotaPeriod period, long amount, Instant timestamp) {
        PeriodKey periodKey = periodKeyCalculator.calculate(period, timestamp);

        // Update Redis
        quotaRedisAdapter.incrementUsage(tenantId, metric, period, periodKey, amount);

        // Update PostgreSQL durable record
        Optional<UsageRecord> recordOpt = usageRecordRepository
                .findByTenantIdAndQuotaMetricAndQuotaPeriodAndPeriodKey(tenantId, metric, period, periodKey.value());

        UsageRecord record;
        if (recordOpt.isPresent()) {
            record = recordOpt.get();
            record.setUsageValue(record.getUsageValue() + amount);
        } else {
            record = new UsageRecord();
            record.setTenantId(tenantId);
            record.setQuotaMetric(metric);
            record.setQuotaPeriod(period);
            record.setPeriodKey(periodKey.value());
            record.setUsageValue(amount);
        }
        usageRecordRepository.save(record);

        log.debug("Recorded usage for tenant: {}, metric: {}, period: {}, key: {}, amount: {}",
                tenantId, metric, period, periodKey.value(), amount);
    }
}
