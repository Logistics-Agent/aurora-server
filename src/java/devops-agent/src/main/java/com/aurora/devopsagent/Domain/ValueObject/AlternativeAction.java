package com.aurora.devopsagent.Domain.ValueObject;

import com.aurora.devopsagent.Domain.Enums.ActionType;
import java.util.Map;

public record AlternativeAction(
    ActionType actionType,
    String actionDescription,
    Map<String, Object> actionParams,
    double confidence,
    RiskLevel risk,
    String reason
) {}
