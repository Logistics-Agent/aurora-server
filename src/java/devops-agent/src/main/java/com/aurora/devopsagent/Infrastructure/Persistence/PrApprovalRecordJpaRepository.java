package com.aurora.devopsagent.Infrastructure.Persistence;

import com.aurora.devopsagent.Domain.Entity.PrApprovalRecord;
import com.aurora.devopsagent.Domain.Enums.ApprovalStatus;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface PrApprovalRecordJpaRepository extends JpaRepository<PrApprovalRecord, UUID> {
    List<PrApprovalRecord> findByIncidentId(UUID incidentId);
    List<PrApprovalRecord> findByStatus(ApprovalStatus status);
}
