package com.aurora.devopsagent.Application.Services;

import com.aurora.devopsagent.Domain.Enums.ActionType;

import java.util.Map;

public record ActionRequest(
    String correlationId,
    ActionType actionType,
    Map<String, Object> params,
    String targetService,
    String targetNamespace,
    boolean dryRun
) {}
