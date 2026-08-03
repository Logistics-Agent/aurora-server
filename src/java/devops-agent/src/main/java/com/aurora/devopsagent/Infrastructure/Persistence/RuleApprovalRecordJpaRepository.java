package com.aurora.devopsagent.Infrastructure.Persistence;

import com.aurora.devopsagent.Domain.Entity.RuleApprovalRecord;
import com.aurora.devopsagent.Domain.Enums.ApprovalStatus;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface RuleApprovalRecordJpaRepository extends JpaRepository<RuleApprovalRecord, UUID> {
    List<RuleApprovalRecord> findByPendingRuleId(UUID pendingRuleId);
    List<RuleApprovalRecord> findByStatus(ApprovalStatus status);
}
