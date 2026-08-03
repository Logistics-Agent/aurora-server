package com.aurora.devopsagent.Application.Services.Providers;

import com.aurora.devopsagent.Domain.ValueObject.MetricSnapshot;

import java.time.Instant;
import java.util.List;

public interface MetricProvider {
    String getProviderName();
    List<MetricSnapshot> fetchMetrics(String service, String namespace, Instant startTime, Instant endTime);
}
