package com.aurora.devopsagent.Application.Services.Providers;

import com.aurora.devopsagent.Domain.ValueObject.KubernetesEvent;
import com.aurora.devopsagent.Domain.ValueObject.PodStatus;

import java.time.Instant;
import java.util.List;

public interface ClusterEventProvider {
    String getProviderName();
    List<KubernetesEvent> fetchClusterEvents(String namespace, Instant startTime, Instant endTime);
    PodStatus fetchPodStatus(String service, String namespace);
}
