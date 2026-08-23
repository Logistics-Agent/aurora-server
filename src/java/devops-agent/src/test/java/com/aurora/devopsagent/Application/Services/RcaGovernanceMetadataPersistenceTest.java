package com.aurora.devopsagent.Application.Services;

import com.aurora.devopsagent.Domain.Entity.Incident;
import com.aurora.devopsagent.Domain.Entity.RcaAnalysis;
import com.aurora.devopsagent.Domain.Enums.IncidentStatus;
import com.aurora.devopsagent.Domain.Enums.Severity;
import com.aurora.devopsagent.Domain.ValueObject.RedactedIncidentContext;
import com.aurora.devopsagent.Infrastructure.AI.AiGovernanceClient;
import com.aurora.devopsagent.Infrastructure.Audit.AuditEventOutboxService;
import com.aurora.devopsagent.Infrastructure.Persistence.IncidentJpaRepository;
import com.aurora.devopsagent.Infrastructure.Persistence.RcaAnalysisJpaRepository;
import com.aurora.devopsagent.Infrastructure.RAG.DevOpsRagClient;
import com.aurora.devopsagent.Infrastructure.Security.RedactionService;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.Collections;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.*;

class RcaGovernanceMetadataPersistenceTest {

    private IncidentJpaRepository incidentRepository;
    private RcaAnalysisJpaRepository rcaAnalysisRepository;
    private RedactionService redactionService;
    private DevOpsRagClient ragClient;
    private AiGovernanceClient aiGovernanceClient;
    private AuditEventOutboxService outboxService;
    private RcaOrchestratorService rcaOrchestratorService;

    @BeforeEach
    void setUp() {
        incidentRepository = mock(IncidentJpaRepository.class);
        rcaAnalysisRepository = mock(RcaAnalysisJpaRepository.class);
        redactionService = mock(RedactionService.class);
        ragClient = mock(DevOpsRagClient.class);
        aiGovernanceClient = mock(AiGovernanceClient.class);
        outboxService = mock(AuditEventOutboxService.class);

        when(incidentRepository.save(any(Incident.class))).thenAnswer(i -> i.getArgument(0));
        when(rcaAnalysisRepository.save(any(RcaAnalysis.class))).thenAnswer(i -> i.getArgument(0));
        when(redactionService.redact(anyString())).thenAnswer(i -> i.getArgument(0));
        when(ragClient.queryKnowledge(any(RedactedIncidentContext.class)))
                .thenReturn(DevOpsRagClient.RetrievalResult.success(Collections.emptyList()));

        rcaOrchestratorService = new RcaOrchestratorService(
                incidentRepository,
                rcaAnalysisRepository,
                redactionService,
                ragClient,
                aiGovernanceClient,
                outboxService
        );
    }

    @Test
    @DisplayName("AiGovernance decisionId, automationLevel, requiresApproval, input/output tokens are persisted into RcaAnalysis")
    void testAiGovernanceMetadataPersisted() {
        Incident incident = new Incident();
        incident.setCorrelationId("corr-gov-meta-1");
        incident.setDedupKey("dedup-gov-meta-1");
        incident.setSource("azure_monitor");
        incident.setErrorSignature("NullPointerException in AuthService");
        incident.escalateSeverity(Severity.High);
        incident.setAffectedService("AuthService");

        incident.transitionTo(IncidentStatus.COLLECTING_CONTEXT);
        incident.transitionTo(IncidentStatus.CONTEXT_READY);

        AiGovernanceClient.GenerateResult mockResult = new AiGovernanceClient.GenerateResult(
                "Root Cause: NPE at token validator",
                350,
                120,
                "decision-xyz-789",
                "HUMAN_IN_THE_LOOP",
                true,
                "gpt-4o",
                "azure_openai"
        );
        when(aiGovernanceClient.generate(any(AiGovernanceClient.GenerateCommand.class))).thenReturn(mockResult);

        RcaAnalysis analysis = rcaOrchestratorService.executeRca(incident, "{}");

        assertNotNull(analysis);
        assertEquals("decision-xyz-789", analysis.getGovernanceDecisionId());
        assertEquals("HUMAN_IN_THE_LOOP", analysis.getAutomationLevel());
        assertTrue(analysis.isRequiresApproval());
        assertEquals(350, analysis.getInputTokens());
        assertEquals(120, analysis.getOutputTokens());
        assertEquals(470, analysis.getLlmTokensUsed());
        assertTrue(analysis.isRagAugmented());
    }
}
