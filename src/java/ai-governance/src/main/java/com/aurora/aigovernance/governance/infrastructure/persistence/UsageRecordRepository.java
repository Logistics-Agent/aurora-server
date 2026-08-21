package com.aurora.aigovernance.governance.infrastructure.persistence;

import com.aurora.aigovernance.governance.domain.entity.UsageRecord;
import com.aurora.aigovernance.governance.domain.enums.QuotaMetric;
import com.aurora.aigovernance.governance.domain.enums.QuotaPeriod;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.Optional;
import java.util.UUID;

@Repository
public interface UsageRecordRepository extends JpaRepository<UsageRecord, UUID> {

    Optional<UsageRecord> findByTenantIdAndQuotaMetricAndQuotaPeriodAndPeriodKey(
            UUID tenantId,
            QuotaMetric quotaMetric,
            QuotaPeriod quotaPeriod,
            String periodKey
    );
}
