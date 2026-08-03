package com.aurora.devopsagent.Application.Queries;

import com.aurora.devopsagent.Domain.Entity.Incident;
import com.aurora.devopsagent.Domain.Enums.Severity;
import com.aurora.devopsagent.Infrastructure.Persistence.IncidentJpaRepository;
import com.aurora.shared.exception.DomainExceptions;
import com.aurora.shared.pagination.GrpcPaginationUtils;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class GetIncidentQueryHandler {

    private final IncidentJpaRepository incidentRepository;

    public GetIncidentQueryHandler(IncidentJpaRepository incidentRepository) {
        this.incidentRepository = incidentRepository;
    }

    @Transactional(readOnly = true)
    public Incident getByCorrelationId(String correlationId) {
        return incidentRepository.findByCorrelationId(correlationId)
                .orElseThrow(() -> new DomainExceptions.NotFoundException("Incident not found for correlationId: " + correlationId));
    }
}
