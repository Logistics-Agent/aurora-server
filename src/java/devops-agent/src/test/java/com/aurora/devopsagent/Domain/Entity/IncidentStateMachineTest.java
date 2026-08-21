package com.aurora.devopsagent.Domain.Entity;

import com.aurora.devopsagent.Domain.Enums.IncidentStatus;
import com.aurora.devopsagent.Domain.Enums.Severity;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.*;

class IncidentStateMachineTest {

    @Test
    @DisplayName("Initial status should be NEW")
    void testInitialStatus() {
        Incident incident = new Incident();
        assertEquals(IncidentStatus.NEW, incident.getStatus());
    }

    @Test
    @DisplayName("Valid transition flow for Medium/High severity should succeed")
    void testValidTransitions() {
        Incident incident = new Incident();
        incident.escalateSeverity(Severity.Medium);

        assertDoesNotThrow(() -> incident.transitionTo(IncidentStatus.COLLECTING_CONTEXT));
        assertEquals(IncidentStatus.COLLECTING_CONTEXT, incident.getStatus());

        assertDoesNotThrow(() -> incident.transitionTo(IncidentStatus.CONTEXT_READY));
        assertDoesNotThrow(() -> incident.transitionTo(IncidentStatus.AI_ANALYSIS));
        assertDoesNotThrow(() -> incident.transitionTo(IncidentStatus.RECOMMENDATION_READY));
        assertDoesNotThrow(() -> incident.transitionTo(IncidentStatus.WAITING_APPROVAL));
        assertDoesNotThrow(() -> incident.transitionTo(IncidentStatus.APPROVED));
        assertDoesNotThrow(() -> incident.transitionTo(IncidentStatus.EXECUTING));
        assertDoesNotThrow(() -> incident.transitionTo(IncidentStatus.VERIFYING));
        assertDoesNotThrow(() -> incident.transitionTo(IncidentStatus.RESOLVED));
        assertDoesNotThrow(() -> incident.transitionTo(IncidentStatus.CLOSED));

        assertEquals(IncidentStatus.CLOSED, incident.getStatus());
    }

    @Test
    @DisplayName("LOW severity incident must be blocked from AI_ANALYSIS (no-LLM guard)")
    void testLowSeverityBlockedFromAiAnalysis() {
        Incident incident = new Incident();
        incident.escalateSeverity(Severity.Low);

        incident.transitionTo(IncidentStatus.COLLECTING_CONTEXT);
        incident.transitionTo(IncidentStatus.CONTEXT_READY);

        IllegalStateException ex = assertThrows(
                IllegalStateException.class,
                () -> incident.transitionTo(IncidentStatus.AI_ANALYSIS)
        );

        assertTrue(ex.getMessage().contains("no-LLM guard"));

        // But LOW can transition to RULE_ANALYSIS
        assertDoesNotThrow(() -> incident.transitionTo(IncidentStatus.RULE_ANALYSIS));
        assertEquals(IncidentStatus.RULE_ANALYSIS, incident.getStatus());
    }

    @Test
    @DisplayName("Invalid transition should throw IllegalStateException")
    void testInvalidTransitionThrows() {
        Incident incident = new Incident();
        assertEquals(IncidentStatus.NEW, incident.getStatus());

        IllegalStateException ex = assertThrows(
            IllegalStateException.class,
            () -> incident.transitionTo(IncidentStatus.RESOLVED)
        );

        assertTrue(ex.getMessage().contains("Invalid transition"));
    }

    @Test
    @DisplayName("Severity escalation should succeed while downgrade should fail")
    void testSeverityEscalationInvariants() {
        Incident incident = new Incident();
        incident.escalateSeverity(Severity.Low);
        assertEquals(Severity.Low, incident.getSeverity());

        // Escalation to High
        assertDoesNotThrow(() -> incident.escalateSeverity(Severity.High));
        assertEquals(Severity.High, incident.getSeverity());

        // Downgrade to Low should throw IllegalArgumentException
        IllegalArgumentException ex = assertThrows(
            IllegalArgumentException.class,
            () -> incident.escalateSeverity(Severity.Low)
        );

        assertTrue(ex.getMessage().contains("Cannot downgrade severity"));
        assertEquals(Severity.High, incident.getSeverity());
    }
}
