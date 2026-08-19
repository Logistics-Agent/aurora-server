package com.aurora.devopsagent.Infrastructure.Persistence;

import com.aurora.devopsagent.Domain.Entity.AuditEventOutbox;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface AuditEventOutboxJpaRepository extends JpaRepository<AuditEventOutbox, UUID> {
    List<AuditEventOutbox> findByProcessedFalseOrderByCreatedAtAsc(Pageable pageable);
}
