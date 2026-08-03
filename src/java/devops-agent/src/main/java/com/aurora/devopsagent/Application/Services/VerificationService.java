package com.aurora.devopsagent.Application.Services;

public interface VerificationService {
    VerificationResult verify(ActionRequest request, ExecutionResult execution);
}
