package com.aurora.devopsagent.Application.Services;

import com.aurora.devopsagent.Domain.Enums.ActionType;

/**
 * SPI for action execution in DevOps-Agent.
 * All remediation actions (Kubernetes, ArgoCD, GitHub, Redis, etc.) must implement this interface.
 */
public interface ActionExecutor {
    /** Check if this executor can handle the given action type */
    boolean supports(ActionType actionType);

    /** Validate that the action is safe to execute */
    ValidationResult validate(ActionRequest request);

    /** Execute the action */
    ExecutionResult execute(ActionRequest request);

    /** Verify that the action had the desired effect */
    VerificationResult verify(ActionRequest request, ExecutionResult result);

    /** Attempt to rollback the action */
    RollbackResult rollback(ActionRequest request, ExecutionResult result);
}
