package com.aurora.devopsagent.Infrastructure.Persistence;

import com.aurora.devopsagent.Domain.Entity.Incident;
import com.aurora.devopsagent.Domain.Enums.IncidentStatus;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface IncidentWorkerLockingRepository extends JpaRepository<Incident, UUID> {

    /**
     * Atomically locks and returns incidents in a specific status across multi-pod replicas
     * using Postgres SELECT FOR UPDATE SKIP LOCKED to prevent concurrent processing.
     */
    @Query(value = "SELECT * FROM incidents WHERE status = :#{#status.name()} ORDER BY created_at ASC LIMIT :limit FOR UPDATE SKIP LOCKED", nativeQuery = true)
    List<Incident> findAndLockIncidentsByStatus(@Param("status") IncidentStatus status, @Param("limit") int limit);
}
