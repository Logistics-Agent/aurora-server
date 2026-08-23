package com.aurora.devopsagent.Infrastructure.Persistence;

import com.aurora.devopsagent.Domain.Entity.RcaAnalysis;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface RcaAnalysisJpaRepository extends JpaRepository<RcaAnalysis, UUID> {
    List<RcaAnalysis> findByIncidentId(UUID incidentId);
    List<RcaAnalysis> findByCorrelationId(String correlationId);
}
