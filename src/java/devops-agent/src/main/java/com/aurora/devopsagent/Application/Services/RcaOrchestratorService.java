package com.aurora.devopsagent.Application.Services;

import com.aurora.devopsagent.Domain.Entity.Incident;
import com.aurora.devopsagent.Domain.Entity.RcaAnalysis;
import com.aurora.devopsagent.Domain.Enums.AuditActionType;
import com.aurora.devopsagent.Domain.Enums.IncidentStatus;
import com.aurora.devopsagent.Domain.Enums.RcaAnalysisStatus;
import com.aurora.devopsagent.Domain.Enums.RcaAnalysisType;
import com.aurora.devopsagent.Domain.Enums.Severity;
import com.aurora.devopsagent.Domain.ValueObject.RedactedIncidentContext;
import com.aurora.devopsagent.Infrastructure.AI.AiGovernanceClient;
import com.aurora.devopsagent.Infrastructure.Audit.AuditEventOutboxService;
import com.aurora.devopsagent.Infrastructure.Persistence.IncidentJpaRepository;
import com.aurora.devopsagent.Infrastructure.Persistence.RcaAnalysisJpaRepository;
import com.aurora.devopsagent.Infrastructure.RAG.DevOpsRagClient;
import com.aurora.devopsagent.Infrastructure.Security.RedactionService;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.math.BigDecimal;
import java.util.Map;
import java.util.stream.Collectors;

/**
 * RcaOrchestratorService: Implements the DevOps RCA Target Pipeline:
 * Incident -> Redaction -> RedactedIncidentContext -> DevOpsRag -> AiGovernance.Generate("devops.rca") -> RcaAnalysis.
 */
@Service
public class RcaOrchestratorService {

    private static final Logger log = LoggerFactory.getLogger(RcaOrchestratorService.class);
    private static final String RCA_CAPABILITY_CODE = "devops.rca";

    private final IncidentJpaRepository incidentRepository;
    private final RcaAnalysisJpaRepository rcaAnalysisRepository;
    private final RedactionService redactionService;
    private final DevOpsRagClient ragClient;
    private final AiGovernanceClient aiGovernanceClient;
    private final AuditEventOutboxService outboxService;

    public RcaOrchestratorService(
            IncidentJpaRepository incidentRepository,
            RcaAnalysisJpaRepository rcaAnalysisRepository,
            RedactionService redactionService,
            DevOpsRagClient ragClient,
            AiGovernanceClient aiGovernanceClient,
            AuditEventOutboxService outboxService) {
        this.incidentRepository = incidentRepository;
        this.rcaAnalysisRepository = rcaAnalysisRepository;
        this.redactionService = redactionService;
        this.ragClient = ragClient;
        this.aiGovernanceClient = aiGovernanceClient;
        this.outboxService = outboxService;
    }

    /**
     * Run full RCA pipeline for an incident.
     */
    @Transactional
    public RcaAnalysis executeRca(Incident incident, String rawEvidenceJson) {
        // Defense-in-depth second guard: LOW severity incidents must NEVER call LLM
        if (incident.getSeverity() == Severity.Low) {
            log.info("LOW severity incident {} cannot enter RCA LLM pipeline (no-LLM guard).", incident.getCorrelationId());
            incident.transitionTo(IncidentStatus.RULE_ANALYSIS);
            incidentRepository.save(incident);
            return null;
        }

        long startTime = System.currentTimeMillis();

        // 1. Transition to AI_ANALYSIS
        incident.transitionTo(IncidentStatus.AI_ANALYSIS);
        incidentRepository.save(incident);

        outboxService.enqueue(
                incident.getCorrelationId(),
                incident.getId(),
                AuditActionType.RCA_ANALYSIS_STARTED,
                String.format("{\"severity\":\"%s\",\"affectedService\":\"%s\"}", incident.getSeverity(), incident.getAffectedService())
        );

        // 2. Mandatory Redaction before RAG and AI
        String sanitizedEvidence = redactionService.redact(rawEvidenceJson != null ? rawEvidenceJson : "{}");
        String sanitizedSignature = redactionService.redact(incident.getErrorSignature());
        RedactedIncidentContext redactedContext = RedactedIncidentContext.of(
                incident.getCorrelationId(),
                sanitizedSignature,
                incident.getAffectedService(),
                sanitizedEvidence,
                ""
        );

        // 3. Query DevOps-RAG using RedactedIncidentContext
        DevOpsRagClient.RetrievalResult ragResult = ragClient.queryKnowledge(redactedContext);
        boolean ragAugmented = ragResult.success();
        String ragFailureReason = ragResult.failureReason();

        String knowledgeContext = ragResult.snippets().stream()
                .map(k -> String.format("- [%s]: %s", k.getTitle(), k.getContent()))
                .collect(Collectors.joining("\n"));

        // 4. Build Governed Prompt
        String prompt = buildRcaPrompt(incident, redactedContext, knowledgeContext);

        // 5. Call AiGovernance.Generate
        AiGovernanceClient.GenerateCommand command = new AiGovernanceClient.GenerateCommand(
                RCA_CAPABILITY_CODE,
                prompt,
                4000,
                1500,
                Map.of(
                        "severity", incident.getSeverity().name(),
                        "affectedService", incident.getAffectedService() != null ? incident.getAffectedService() : "unknown",
                        "ragAugmented", String.valueOf(ragAugmented)
                )
        );

        AiGovernanceClient.GenerateResult generateResult = aiGovernanceClient.generate(command);

        long durationMs = System.currentTimeMillis() - startTime;

        // 6. Create RcaAnalysis record with full AiGovernance metadata
        RcaAnalysis analysis = new RcaAnalysis();
        analysis.setIncident(incident);
        analysis.setCorrelationId(incident.getCorrelationId());
        analysis.setAnalysisType(RcaAnalysisType.APPLICATION);
        analysis.setStatus(RcaAnalysisStatus.COMPLETED);
        analysis.setRecommendationJson(String.format("{\"summary\":\"%s\",\"model\":\"%s\"}",
                escapeJson(generateResult.content()), generateResult.model()));
        analysis.setConfidence(BigDecimal.valueOf(0.92));
        analysis.setLlmTokensUsed((int) (generateResult.inputTokens() + generateResult.outputTokens()));
        analysis.setDurationMs(durationMs);

        // RAG and Governance Traceability Fields
        analysis.setRagAugmented(ragAugmented);
        analysis.setRagFailureReason(ragFailureReason);
        analysis.setGovernanceDecisionId(generateResult.decisionId());
        analysis.setAutomationLevel(generateResult.automationLevel());
        analysis.setRequiresApproval(generateResult.requiresApproval());
        analysis.setInputTokens(generateResult.inputTokens());
        analysis.setOutputTokens(generateResult.outputTokens());

        RcaAnalysis savedAnalysis = rcaAnalysisRepository.save(analysis);

        // 7. Update Incident with RCA results
        incident.setRcaRootCause("Root cause identified via AiGovernance: " + generateResult.model());
        incident.setRcaRecommendation(generateResult.content());

        // 8. State transition based on approval requirement
        if (generateResult.requiresApproval() || incident.getSeverity() == Severity.Critical || incident.getSeverity() == Severity.High) {
            incident.transitionTo(IncidentStatus.RECOMMENDATION_READY);
            incident.transitionTo(IncidentStatus.WAITING_APPROVAL);
        } else {
            incident.transitionTo(IncidentStatus.RECOMMENDATION_READY);
        }

        incidentRepository.save(incident);

        // 9. Transactional Audit Outbox
        outboxService.enqueue(
                incident.getCorrelationId(),
                incident.getId(),
                AuditActionType.RCA_ANALYSIS_COMPLETED,
                String.format("{\"decisionId\":\"%s\",\"tokens\":%d,\"durationMs\":%d,\"ragAugmented\":%b}",
                        generateResult.decisionId(), analysis.getLlmTokensUsed(), durationMs, ragAugmented)
        );

        log.info("RCA Analysis completed for incident id={}, correlationId={}, tokensUsed={}, ragAugmented={}, decisionId={}",
                incident.getId(), incident.getCorrelationId(), analysis.getLlmTokensUsed(), ragAugmented, generateResult.decisionId());

        return savedAnalysis;
    }

    private String buildRcaPrompt(Incident incident, RedactedIncidentContext context, String knowledgeContext) {
        return String.format(
                """
                You are the Aurora Autonomous DevOps RCA Specialist.
                Analyze the following incident and provide a root cause analysis and specific remediation action.

                [INCIDENT DETAILS]
                - Affected Service: %s
                - Severity: %s
                - Error Signature: %s

                [EVIDENCE & LOGS (SANITIZED)]
                %s

                [RELEVANT KNOWLEDGE & RUNBOOKS]
                %s

                Provide your analysis with Root Cause, Impact, Recommended Action (RESTART_POD, ROLLBACK_RELEASE, ADJUST_CONFIG, or CLEAR_CACHE), and Confidence.
                """,
                incident.getAffectedService(),
                incident.getSeverity(),
                context.errorSignature(),
                context.sanitizedContextJson(),
                knowledgeContext.isBlank() ? "No prior matching runbooks found." : knowledgeContext
        );
    }

    private String escapeJson(String input) {
        if (input == null) return "";
        return input.replace("\\", "\\\\")
                .replace("\"", "\\\"")
                .replace("\n", "\\n")
                .replace("\r", "\\r");
    }
}
