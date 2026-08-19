package com.aurora.devopsagent.Application.Services.Providers;

import com.aurora.devopsagent.Domain.ValueObject.TraceSpan;

import java.time.Instant;
import java.util.List;

public interface TraceProvider {
    String getProviderName();
    List<TraceSpan> fetchTraces(String service, String namespace, Instant startTime, Instant endTime, int limit);
}
