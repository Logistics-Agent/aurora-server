package com.aurora.devopsagent.Infrastructure.Security;

import com.aurora.devopsagent.Domain.ValueObject.RedactedIncidentContext;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.*;

class ExternalPayloadRedactionBoundaryTest {

    @Test
    @DisplayName("RedactedIncidentContext encapsulates sanitized state and guarantees no raw secrets pass boundary")
    void testRedactedIncidentContextBoundary() {
        RedactionService service = new RedactionService();

        String rawEvidence = "Failed connect: Server=db;Password=Secret123; user=admin@aurora.io, token=eyJhbGciOiJIUzI1NiJ9.eyJ1c2VyIjoiYWRtaW4ifQ.signature";
        String rawSignature = "Error in auth: Bearer my-secret-bearer-token";

        String sanitizedEvidence = service.redact(rawEvidence);
        String sanitizedSignature = service.redact(rawSignature);

        RedactedIncidentContext context = RedactedIncidentContext.of(
                "corr-redaction-boundary",
                sanitizedSignature,
                "OrderService",
                sanitizedEvidence,
                ""
        );

        assertFalse(context.sanitizedContextJson().contains("Secret123"));
        assertFalse(context.sanitizedContextJson().contains("admin@aurora.io"));
        assertFalse(context.sanitizedContextJson().contains("eyJhbGciOiJIUzI1NiJ9"));
        assertFalse(context.errorSignature().contains("my-secret-bearer-token"));

        assertTrue(context.sanitizedContextJson().contains("[REDACTED_CONNECTION_STRING]"));
        assertTrue(context.sanitizedContextJson().contains("[REDACTED_EMAIL]"));
        assertTrue(context.sanitizedContextJson().contains("[REDACTED_JWT]"));
        assertTrue(context.errorSignature().contains("Bearer [REDACTED_TOKEN]"));
    }
}
