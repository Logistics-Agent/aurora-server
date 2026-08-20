package com.aurora.devopsagent.Application.Services;

import com.aurora.devopsagent.Domain.Entity.Incident;
import com.aurora.devopsagent.Domain.Entity.RcaAnalysis;
import com.aurora.devopsagent.Domain.Enums.AuditActionType;
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
import org.mockito.ArgumentCaptor;

import java.util.Collections;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.*;

class RcaOrchestratorTest {

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
    @DisplayName("RCA calls AiGovernance.Generate with devops.rca capability for High severity incident")
    void testRcaCallsAiGovernance() {
        Incident incident = new Incident();
        incident.setCorrelationId("corr-12345");
        incident.setDedupKey("dedup-12345");
        incident.setSource("azure_monitor");
        incident.setErrorSignature("OutOfMemoryError in PaymentService");
        incident.escalateSeverity(Severity.High);
        incident.setAffectedService("PaymentService");

        incident.transitionTo(IncidentStatus.COLLECTING_CONTEXT);
        incident.transitionTo(IncidentStatus.CONTEXT_READY);

        AiGovernanceClient.GenerateResult mockResult = new AiGovernanceClient.GenerateResult(
                "Root Cause: Memory leak in cache. Recommendation: RESTART_POD and patch v2.1",
                500,
                200,
                "dec-9876",
                "AUTOMATED",
                true,
                "gpt-4o",
                "azure_openai"
        );

        when(aiGovernanceClient.generate(any(AiGovernanceClient.GenerateCommand.class))).thenReturn(mockResult);

        RcaAnalysis result = rcaOrchestratorService.executeRca(incident, "{\"rawLog\":\"OOM\"}");

        assertNotNull(result);
        assertEquals("corr-12345", result.getCorrelationId());
        assertEquals(700, result.getLlmTokensUsed());
        assertEquals("dec-9876", result.getGovernanceDecisionId());
        assertTrue(result.isRagAugmented());

        ArgumentCaptor<AiGovernanceClient.GenerateCommand> captor = ArgumentCaptor.forClass(AiGovernanceClient.GenerateCommand.class);
        verify(aiGovernanceClient, times(1)).generate(captor.capture());

        AiGovernanceClient.GenerateCommand capturedCommand = captor.getValue();
        assertEquals("devops.rca", capturedCommand.capabilityCode());
        assertTrue(capturedCommand.prompt().contains("PaymentService"));
        assertTrue(capturedCommand.prompt().contains("OutOfMemoryError"));

        verify(redactionService, atLeastOnce()).redact(anyString());
        verify(outboxService, times(1)).enqueue(eq("corr-12345"), any(), eq(AuditActionType.RCA_ANALYSIS_STARTED), any());
        verify(outboxService, times(1)).enqueue(eq("corr-12345"), any(), eq(AuditActionType.RCA_ANALYSIS_COMPLETED), any());
    }

    @Test
    @DisplayName("LOW severity incident never invokes AiGovernanceClient (no-LLM guard)")
    void testLowSeverityBypassesAiGovernance() {
        Incident incident = new Incident();
        incident.setCorrelationId("corr-low-123");
        incident.setDedupKey("dedup-low-123");
        incident.setSource("loki");
        incident.setErrorSignature("Minor warn log");
        incident.escalateSeverity(Severity.Low);
        incident.setAffectedService("OrderService");

        incident.transitionTo(IncidentStatus.COLLECTING_CONTEXT);
        incident.transitionTo(IncidentStatus.CONTEXT_READY);

        RcaAnalysis result = rcaOrchestratorService.executeRca(incident, "{}");

        assertNull(result);
        assertEquals(IncidentStatus.RULE_ANALYSIS, incident.getStatus());
        verifyNoInteractions(aiGovernanceClient);
        verifyNoInteractions(ragClient);
    }
}
