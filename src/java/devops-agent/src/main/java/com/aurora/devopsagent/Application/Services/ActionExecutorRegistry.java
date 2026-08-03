package com.aurora.devopsagent.Application.Services;

import com.aurora.devopsagent.Domain.Enums.ActionType;
import org.springframework.stereotype.Service;

import java.util.List;

@Service
public class ActionExecutorRegistry {
    private final List<ActionExecutor> executors;

    public ActionExecutorRegistry(List<ActionExecutor> executors) {
        this.executors = executors;
    }

    public ActionExecutor getExecutor(ActionType type) {
        return executors.stream()
            .filter(e -> e.supports(type))
            .findFirst()
            .orElseThrow(() -> new UnsupportedActionException(type));
    }
}
