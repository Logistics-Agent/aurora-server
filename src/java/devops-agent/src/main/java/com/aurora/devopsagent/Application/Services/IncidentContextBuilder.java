package com.aurora.devopsagent.Application.Services;

import com.aurora.devopsagent.Domain.Entity.Incident;
import com.aurora.devopsagent.Domain.ValueObject.IncidentContext;

public interface IncidentContextBuilder {
    IncidentContext build(Incident incident);
}
