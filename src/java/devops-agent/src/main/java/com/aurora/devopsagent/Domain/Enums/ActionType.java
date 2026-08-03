package com.aurora.devopsagent.Domain.Enums;

public enum ActionType {
    RESTART_POD(false, true),
    SCALE_DEPLOYMENT(false, true),
    ROLLBACK_RELEASE(false, true),
    RESTART_JOB(false, true),
    ADJUST_CONFIG(false, false),
    FLUSH_REDIS_KEY(true, true),
    RESTART_RABBITMQ(true, false),
    CREATE_GITHUB_PR(false, false),
    CREATE_WORK_ITEM(false, false),
    NO_ACTION(false, false);

    private final boolean destructive;
    private final boolean idempotent;

    ActionType(boolean destructive, boolean idempotent) {
        this.destructive = destructive;
        this.idempotent = idempotent;
    }

    public boolean isDestructive() {
        return destructive;
    }

    public boolean isIdempotent() {
        return idempotent;
    }
}
