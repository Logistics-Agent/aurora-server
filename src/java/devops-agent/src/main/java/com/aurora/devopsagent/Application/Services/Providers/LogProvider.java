package com.aurora.devopsagent.Application.Services.Providers;

import com.aurora.devopsagent.Domain.ValueObject.LogEntry;

import java.time.Instant;
import java.util.List;

public interface LogProvider {
    String getProviderName();
    List<LogEntry> fetchLogs(String service, String namespace, Instant startTime, Instant endTime, int limit);
}
