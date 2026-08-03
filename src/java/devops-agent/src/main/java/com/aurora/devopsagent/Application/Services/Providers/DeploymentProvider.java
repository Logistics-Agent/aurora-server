package com.aurora.devopsagent.Application.Services.Providers;

import com.aurora.devopsagent.Domain.ValueObject.ConfigChange;
import com.aurora.devopsagent.Domain.ValueObject.RecentDeployment;

import java.time.Instant;
import java.util.List;

public interface DeploymentProvider {
    String getProviderName();
    List<RecentDeployment> fetchRecentDeployments(String service, String namespace, Instant startTime, Instant endTime);
    List<ConfigChange> fetchConfigChanges(String service, String namespace, Instant startTime, Instant endTime);
}
