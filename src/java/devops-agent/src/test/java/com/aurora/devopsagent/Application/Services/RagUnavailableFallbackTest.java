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
import org.mockito.ArgumentCaptor;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.*;

class RagUnavailableFallbackTest {

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
    @DisplayName("When DevOps-RAG throws UNAVAILABLE, RCA continues with sanitized context and records ragAugmented=false")
    void testRagUnavailableFallback() {
        Incident incident = new Incident();
        incident.setCorrelationId("corr-rag-down");
        incident.setDedupKey("dedup-rag-down");
        incident.setSource("loki");
        incident.setErrorSignature("Connection pool exhausted in BillingService");
        incident.escalateSeverity(Severity.High);
        incident.setAffectedService("BillingService");

        incident.transitionTo(IncidentStatus.COLLECTING_CONTEXT);
        incident.transitionTo(IncidentStatus.CONTEXT_READY);

        // RAG returns fallback
        when(ragClient.queryKnowledge(any(RedactedIncidentContext.class)))
                .thenReturn(DevOpsRagClient.RetrievalResult.fallback("UNAVAILABLE"));

        AiGovernanceClient.GenerateResult mockResult = new AiGovernanceClient.GenerateResult(
                "RCA: HikariCP max pool size reached. Recommendation: Increase pool size to 50",
                600,
                150,
                "dec-gov-001",
                "AUTOMATED",
                false,
                "gpt-4o",
                "azure_openai"
        );
        when(aiGovernanceClient.generate(any(AiGovernanceClient.GenerateCommand.class))).thenReturn(mockResult);

        RcaAnalysis analysis = rcaOrchestratorService.executeRca(incident, "{\"pool\":\"HikariCP\"}");

        assertNotNull(analysis);
        // Verify RAG fallback metadata
        assertFalse(analysis.isRagAugmented());
        assertEquals("UNAVAILABLE", analysis.getRagFailureReason());

        // Verify AiGovernance was still called
        verify(aiGovernanceClient, times(1)).generate(any(AiGovernanceClient.GenerateCommand.class));
    }
}
