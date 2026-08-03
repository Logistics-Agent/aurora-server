package com.aurora.devopsagent.Domain.ValueObject;

import java.util.List;

public record TopologySnapshot(
    String serviceName,
    List<String> upstreamDependencies,   // Services that call this service
    List<String> downstreamDependencies, // Services this service calls
    String clusterName,
    String namespace
) {}
