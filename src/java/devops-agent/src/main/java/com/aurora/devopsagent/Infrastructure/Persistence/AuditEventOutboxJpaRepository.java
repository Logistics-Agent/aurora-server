package com.aurora.devopsagent.Infrastructure.Persistence;

import com.aurora.devopsagent.Domain.Entity.AuditEventOutbox;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface AuditEventOutboxJpaRepository extends JpaRepository<AuditEventOutbox, UUID> {

    List<AuditEventOutbox> findByProcessedFalseOrderByCreatedAtAsc(Pageable pageable);

    /**
     * Atomically locks and returns pending outbox records across multi-pod replicas
     * using Postgres SELECT FOR UPDATE SKIP LOCKED.
     */
    @Query(value = "SELECT * FROM audit_event_outbox WHERE processed = false ORDER BY created_at ASC LIMIT :limit FOR UPDATE SKIP LOCKED", nativeQuery = true)
    List<AuditEventOutbox> findAndLockPendingBatch(@Param("limit") int limit);
}
