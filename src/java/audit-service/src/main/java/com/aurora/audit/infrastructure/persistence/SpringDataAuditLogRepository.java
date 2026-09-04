package com.aurora.audit.infrastructure.persistence;

import com.aurora.audit.infrastructure.persistence.entity.AuditLogEntity;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.JpaSpecificationExecutor;
import org.springframework.stereotype.Repository;

import java.util.List;

@Repository
public interface SpringDataAuditLogRepository extends JpaRepository<AuditLogEntity, String>, JpaSpecificationExecutor<AuditLogEntity> {
    List<AuditLogEntity> findByServiceNameOrderByCreatedAtDesc(String serviceName);
    List<AuditLogEntity> findByTenantIdOrderByCreatedAtDesc(String tenantId);
    Page<AuditLogEntity> findByServiceNameAndEventType(String serviceName, String eventType, Pageable pageable);
    Page<AuditLogEntity> findAllByOrderByCreatedAtDesc(Pageable pageable);
}
