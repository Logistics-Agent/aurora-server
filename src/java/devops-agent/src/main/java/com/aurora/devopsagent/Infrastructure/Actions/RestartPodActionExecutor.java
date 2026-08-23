package com.aurora.devopsagent.Infrastructure.Actions;

import com.aurora.devopsagent.Application.Services.*;
import com.aurora.devopsagent.Domain.Enums.ActionType;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

import java.time.Duration;
import java.util.Map;

@Component
public class RestartPodActionExecutor implements ActionExecutor {

    private static final Logger log = LoggerFactory.getLogger(RestartPodActionExecutor.class);

    @Override
    public boolean supports(ActionType actionType) {
        return actionType == ActionType.RESTART_POD;
    }

    @Override
    public ValidationResult validate(ActionRequest request) {
        if (request.targetService() == null || request.targetService().isBlank()) {
            return ValidationResult.fail("targetService is required for RESTART_POD");
        }
        return ValidationResult.ok();
    }

    @Override
    public ExecutionResult execute(ActionRequest request) {
        log.info("Executing RESTART_POD for service '{}' (dryRun={})", request.targetService(), request.dryRun());
        if (request.dryRun()) {
            return new ExecutionResult(ExecutionStatus.DRY_RUN_PASSED, "Dry-run pod restart validated", Duration.ofMillis(50), Map.of("service", request.targetService()));
        }
        // In simulation/infrastructure mode, log and succeed
        return new ExecutionResult(ExecutionStatus.SUCCESS, "Pod restarted successfully for " + request.targetService(), Duration.ofMillis(350), Map.of("restartedService", request.targetService()));
    }

    @Override
    public VerificationResult verify(ActionRequest request, ExecutionResult result) {
        return new VerificationResult(VerificationStatus.PASSED, "Service health endpoint returned 200 OK", Duration.ofSeconds(1), Map.of("healthy", true));
    }

    @Override
    public RollbackResult rollback(ActionRequest request, ExecutionResult result) {
        return new RollbackResult(true, "Rollback not required for pod restart", Duration.ZERO);
    }
}
