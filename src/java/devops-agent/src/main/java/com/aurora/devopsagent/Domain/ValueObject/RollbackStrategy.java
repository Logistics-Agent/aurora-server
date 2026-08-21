package com.aurora.devopsagent.Domain.ValueObject;

import com.aurora.devopsagent.Domain.Enums.ActionType;
import java.util.Map;

public record RollbackStrategy(
    ActionType rollbackActionType,      // e.g., ROLLBACK_RELEASE
    Map<String, Object> rollbackParams, // e.g., {"targetVersion": "v2.3.0"}
    String rollbackDescription,         // "Re-deploy previous Helm release revision"
    int estimatedRollbackMinutes,
    boolean automatic                   // true = ActionExecutor auto-rollbacks on verification failure
) {}
