package com.aurora.devopsagent.Domain.Enums;

import java.time.Duration;

public enum Severity {
    Low(1, Duration.ofMinutes(5), 20, Duration.ofMinutes(30)),
    Medium(2, Duration.ofMinutes(10), 30, Duration.ofHours(2)),
    High(3, Duration.ofMinutes(15), 40, Duration.ofHours(6)),
    Critical(4, Duration.ofMinutes(20), 50, Duration.ofHours(12));

    private final int weight;
    private final Duration contextWindow;
    private final int evidenceBudget;
    private final Duration approvalTimeout;

    Severity(int weight, Duration contextWindow, int evidenceBudget, Duration approvalTimeout) {
        this.weight = weight;
        this.contextWindow = contextWindow;
        this.evidenceBudget = evidenceBudget;
        this.approvalTimeout = approvalTimeout;
    }

    public int weight() {
        return weight;
    }

    public Duration contextWindow() {
        return contextWindow;
    }

    public int evidenceBudget() {
        return evidenceBudget;
    }

    public Duration approvalTimeout() {
        return approvalTimeout;
    }
}

