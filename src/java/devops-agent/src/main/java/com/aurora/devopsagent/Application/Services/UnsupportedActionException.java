package com.aurora.devopsagent.Application.Services;

import com.aurora.devopsagent.Domain.Enums.ActionType;

public class UnsupportedActionException extends RuntimeException {
    public UnsupportedActionException(ActionType actionType) {
        super("No ActionExecutor found for action type: " + actionType);
    }
}
