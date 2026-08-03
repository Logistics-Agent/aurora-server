package com.aurora.devopsagent.Application.Services;

import com.aurora.devopsagent.Domain.Entity.Incident;
import com.aurora.devopsagent.Domain.Entity.RcaAnalysis;

public interface KnowledgeFeedbackService {
    /**
     * Called when Incident transitions to RESOLVED.
     * Validates, sanitizes, and ingests knowledge.
     */
    void processResolvedIncident(Incident incident, RcaAnalysis analysis);
}
