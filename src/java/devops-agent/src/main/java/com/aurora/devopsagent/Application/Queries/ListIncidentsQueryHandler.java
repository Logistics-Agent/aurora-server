package com.aurora.devopsagent.Application.Queries;

import com.aurora.devopsagent.Domain.Entity.Incident;
import com.aurora.devopsagent.Domain.Enums.Severity;
import com.aurora.devopsagent.Infrastructure.Persistence.IncidentJpaRepository;
import com.aurora.shared.pagination.GrpcPaginationUtils;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class ListIncidentsQueryHandler {

    private final IncidentJpaRepository incidentRepository;

    public ListIncidentsQueryHandler(IncidentJpaRepository incidentRepository) {
        this.incidentRepository = incidentRepository;
    }

    @Transactional(readOnly = true)
    public Page<Incident> handle(String severityStr, int pageNumber, int pageSize) {
        Pageable pageable = GrpcPaginationUtils.toPageable(pageNumber, pageSize);
        if (severityStr != null && !severityStr.trim().isEmpty()) {
            try {
                Severity severity = Severity.valueOf(severityStr.trim());
                return incidentRepository.findBySeverity(severity, pageable);
            } catch (IllegalArgumentException e) {
                // Ignore invalid enum
            }
        }
        return incidentRepository.findAll(pageable);
    }
}
