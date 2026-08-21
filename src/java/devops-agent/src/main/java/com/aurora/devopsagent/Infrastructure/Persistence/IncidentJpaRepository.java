package com.aurora.devopsagent.Infrastructure.Persistence;

import com.aurora.devopsagent.Domain.Entity.Incident;
import com.aurora.devopsagent.Domain.Enums.Severity;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.Optional;
import java.util.UUID;

/**
 * Direct JPA Table repository for Incident (acts as DbSet<Incident> in CQRS handlers).
 */
@Repository
public interface IncidentJpaRepository extends JpaRepository<Incident, UUID> {
    Optional<Incident> findByCorrelationId(String correlationId);
    Optional<Incident> findByDedupKey(String dedupKey);
    Page<Incident> findBySeverity(Severity severity, Pageable pageable);
}
