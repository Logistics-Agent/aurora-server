package com.aurora.aigovernance.orchestration.application;

import com.aurora.aigovernance.gateway.application.execution.AiExecutionService;
import com.aurora.aigovernance.gateway.domain.valueobject.AiEmbeddingRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiEmbeddingResult;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateResult;
import com.aurora.aigovernance.governance.application.port.PolicyAuditPort;
import com.aurora.aigovernance.governance.application.service.GovernancePolicyService;
import com.aurora.aigovernance.governance.domain.valueobject.GovernanceDecision;
import com.aurora.aigovernance.governance.infrastructure.messaging.AiUsageEvent;
import com.aurora.aigovernance.governance.infrastructure.messaging.RabbitMqConfig;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import io.micrometer.core.instrument.MeterRegistry;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.amqp.rabbit.core.RabbitTemplate;
import org.springframework.stereotype.Service;

import java.time.Instant;
import java.util.UUID;

/**
 * Cross-module orchestrator coordinating Governance and Gateway modules.
 * <p>
 * Exposes explicit {@link #generate(GenerateAiCommand)} and {@link #embed(EmbedAiCommand)} methods.
 * Contains no ThreadLocal logic — operates entirely on command parameters.
 */
@Service
public class ExecuteAiService {

    private static final Logger log = LoggerFactory.getLogger(ExecuteAiService.class);

    private final GovernancePolicyService governancePolicyService;
    private final AiExecutionService aiExecutionService;
    private final PolicyAuditPort policyAuditPort;
    private final RabbitTemplate rabbitTemplate;
    private final MeterRegistry meterRegistry;

    public ExecuteAiService(
            GovernancePolicyService governancePolicyService,
            AiExecutionService aiExecutionService,
            PolicyAuditPort policyAuditPort,
            RabbitTemplate rabbitTemplate,
            MeterRegistry meterRegistry) {
        this.governancePolicyService = governancePolicyService;
        this.aiExecutionService = aiExecutionService;
        this.policyAuditPort = policyAuditPort;
        this.rabbitTemplate = rabbitTemplate;
        this.meterRegistry = meterRegistry;
    }

    /**
     * Orchestrate AI Generation.
     */
    public GovernedGenerateResult generate(GenerateAiCommand command) {
        long startTime = System.currentTimeMillis();

        // 1. Governance policy evaluation
        GovernanceDecision decision = governancePolicyService.evaluate(
                command.tenantId(),
                command.callerServiceId(),
                command.capabilityCode(),
                AiOperation.GENERATE,
                command.tokenBudget()
        );

        // Audit policy decision
        policyAuditPort.publishPolicyDecision(new PolicyAuditPort.PolicyAuditEvent(
                decision.decisionId(),
                command.tenantId() != null ? command.tenantId().toString() : "anonymous",
                command.callerServiceId(),
                command.capabilityCode(),
                AiOperation.GENERATE.name(),
                decision.allowed(),
                decision.denyReason() != null ? decision.denyReason().name() : null,
                decision.policyVersion(),
                System.currentTimeMillis()
        ));

        // 2. If denied, return immediately (Gateway is never called)
        if (!decision.allowed()) {
            meterRegistry.counter("governance_policy_denied_total",
                    "capability", command.capabilityCode(),
                    "reason", decision.denyReason().name()).increment();
            return new GovernedGenerateResult(null, decision);
        }

        meterRegistry.counter("governance_policy_allowed_total",
                "capability", command.capabilityCode(),
                "callerServiceId", command.callerServiceId()).increment();

        // 3. Delegate to Gateway for provider execution
        AiGenerateRequest request = new AiGenerateRequest(
                command.capabilityCode(),
                command.prompt(),
                command.tokenBudget().maxOutputTokens(),
                command.tokenBudget().estimatedInputTokens(),
                command.parameters(),
                command.inputParts()
        );

        AiGenerateResult result = aiExecutionService.generate(
                decision,
                request,
                command.callerServiceId(),
                command.tokenBudget()
        );

        long duration = System.currentTimeMillis() - startTime;

        // 4. Metrics & Async usage event
        meterRegistry.counter("ai_gateway_requests_total",
                "operation", "generate",
                "provider", result.provider(),
                "callerServiceId", command.callerServiceId(),
                "result", "success").increment();

        publishUsageEvent(
                command.tenantId(),
                command.userId(),
                command.callerServiceId(),
                command.capabilityCode(),
                AiOperation.GENERATE,
                result.provider(),
                result.model(),
                result.inputTokens(),
                result.outputTokens(),
                duration,
                true
        );

        return new GovernedGenerateResult(result, decision);
    }

    /**
     * Orchestrate AI Embedding.
     */
    public GovernedEmbedResult embed(EmbedAiCommand command) {
        long startTime = System.currentTimeMillis();
        TokenBudget embedBudget = TokenBudget.forEmbedding(command.estimatedInputTokens());

        // 1. Governance policy evaluation
        GovernanceDecision decision = governancePolicyService.evaluate(
                command.tenantId(),
                command.callerServiceId(),
                command.capabilityCode(),
                AiOperation.EMBED,
                embedBudget
        );

        policyAuditPort.publishPolicyDecision(new PolicyAuditPort.PolicyAuditEvent(
                decision.decisionId(),
                command.tenantId() != null ? command.tenantId().toString() : "anonymous",
                command.callerServiceId(),
                command.capabilityCode(),
                AiOperation.EMBED.name(),
                decision.allowed(),
                decision.denyReason() != null ? decision.denyReason().name() : null,
                decision.policyVersion(),
                System.currentTimeMillis()
        ));

        if (!decision.allowed()) {
            meterRegistry.counter("governance_policy_denied_total",
                    "capability", command.capabilityCode(),
                    "reason", decision.denyReason().name()).increment();
            return new GovernedEmbedResult(null, decision);
        }

        meterRegistry.counter("governance_policy_allowed_total",
                "capability", command.capabilityCode(),
                "callerServiceId", command.callerServiceId()).increment();

        // 2. Delegate to Gateway
        AiEmbeddingRequest request = new AiEmbeddingRequest(
                command.capabilityCode(),
                command.content(),
                command.dimensions(),
                command.estimatedInputTokens()
        );

        AiEmbeddingResult result = aiExecutionService.embed(
                decision,
                request,
                command.callerServiceId(),
                embedBudget
        );

        long duration = System.currentTimeMillis() - startTime;

        meterRegistry.counter("ai_gateway_requests_total",
                "operation", "embed",
                "provider", result.provider(),
                "callerServiceId", command.callerServiceId(),
                "result", "success").increment();

        publishUsageEvent(
                command.tenantId(),
                command.userId(),
                command.callerServiceId(),
                command.capabilityCode(),
                AiOperation.EMBED,
                result.provider(),
                result.model(),
                result.inputTokens(),
                0L,
                duration,
                true
        );

        return new GovernedEmbedResult(result, decision);
    }

    private void publishUsageEvent(
            UUID tenantId, UUID userId, String callerServiceId,
            String capabilityCode, AiOperation operation,
            String provider, String model,
            long inputTokens, long outputTokens, long durationMs, boolean success) {
        try {
            AiUsageEvent event = new AiUsageEvent(
                    UUID.randomUUID().toString(),
                    tenantId,
                    userId,
                    callerServiceId,
                    capabilityCode,
                    operation,
                    provider,
                    model,
                    inputTokens,
                    outputTokens,
                    durationMs,
                    success,
                    Instant.now()
            );
            rabbitTemplate.convertAndSend(
                    RabbitMqConfig.EXCHANGE_AI_GOVERNANCE,
                    RabbitMqConfig.ROUTING_KEY_USAGE_EVENTS,
                    event
            );
        } catch (Exception e) {
            log.warn("Failed to publish AI usage event: {}", e.getMessage());
        }
    }

    public record GovernedGenerateResult(AiGenerateResult result, GovernanceDecision decision) {}
    public record GovernedEmbedResult(AiEmbeddingResult result, GovernanceDecision decision) {}
}
