package com.aurora.devopsagent.Infrastructure.Security;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.*;

class RedactionServiceTest {

    private RedactionService redactionService;

    @BeforeEach
    void setUp() {
        redactionService = new RedactionService();
    }

    @Test
    @DisplayName("Redacts JWT tokens")
    void testRedactJwt() {
        String input = "Error authorization token: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c in header";
        String redacted = redactionService.redact(input);

        assertFalse(redacted.contains("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9"));
        assertTrue(redacted.contains("[REDACTED_JWT]"));
    }

    @Test
    @DisplayName("Redacts Bearer tokens")
    void testRedactBearer() {
        String input = "Authorization: Bearer my-super-secret-token-12345.abc_xyz";
        String redacted = redactionService.redact(input);

        assertFalse(redacted.contains("my-super-secret-token-12345.abc_xyz"));
        assertTrue(redacted.contains("Bearer [REDACTED_TOKEN]"));
    }

    @Test
    @DisplayName("Redacts Database connection strings")
    void testRedactConnectionStrings() {
        String input = "Database failed: Server=myServerAddress;Database=myDataBase;Uid=myUsername;Pwd=mySecretPassword123;";
        String redacted = redactionService.redact(input);

        assertFalse(redacted.contains("mySecretPassword123"));
        assertTrue(redacted.contains("[REDACTED_CONNECTION_STRING]"));
    }

    @Test
    @DisplayName("Redacts Gemini, AWS, and generic API keys")
    void testRedactApiKeys() {
        String geminiInput = "Gemini key: AIzaSyD3x9kL1mN0pQ2rS4tU6vW8xY0z1a2b3c4";
        String awsInput = "AWS access key: AKIAIOSFODNN7EXAMPLE";
        String genericInput = "api_key=\"secret-token-abcdef1234567890\"";

        String redactedGemini = redactionService.redact(geminiInput);
        String redactedAws = redactionService.redact(awsInput);
        String redactedGeneric = redactionService.redact(genericInput);

        assertFalse(redactedGemini.contains("AIzaSyD3x9kL1mN0pQ2rS4tU6vW8xY0z1a2b3c4"));
        assertTrue(redactedGemini.contains("[REDACTED_GEMINI_KEY]"));

        assertFalse(redactedAws.contains("AKIAIOSFODNN7EXAMPLE"));
        assertTrue(redactedAws.contains("[REDACTED_AWS_KEY]"));

        assertFalse(redactedGeneric.contains("secret-token-abcdef1234567890"));
        assertTrue(redactedGeneric.contains("[REDACTED_API_KEY]"));
    }

    @Test
    @DisplayName("Redacts PEM Private Key block")
    void testRedactPemPrivateKey() {
        String input = """
                Logs:
                -----BEGIN RSA PRIVATE KEY-----
                MIIEowIBAAKCAQEA0Y3e8rZ...
                -----END RSA PRIVATE KEY-----
                Server crash.
                """;

        String redacted = redactionService.redact(input);

        assertFalse(redacted.contains("MIIEowIBAAKCAQEA0Y3e8rZ"));
        assertTrue(redacted.contains("[REDACTED_PRIVATE_KEY]"));
        assertTrue(redacted.contains("Server crash."));
    }

    @Test
    @DisplayName("Redacts PII emails and phone numbers")
    void testRedactPii() {
        String input = "Contact admin at john.doe@company.com or phone +1-555-123-4567 for urgent incident.";
        String redacted = redactionService.redact(input);

        assertFalse(redacted.contains("john.doe@company.com"));
        assertFalse(redacted.contains("555-123-4567"));
        assertTrue(redacted.contains("[REDACTED_EMAIL]"));
        assertTrue(redacted.contains("[REDACTED_PHONE]"));
    }

    @Test
    @DisplayName("Redacts mixed payload containing multiline logs, secrets, and PII")
    void testRedactMixedPayload() {
        String input = """
                Exception in thread "main" java.sql.SQLException: Access denied for user 'admin' (using password: YES)
                at com.aurora.service.DbPool.connect(DbPool.java:45)
                Context: Server=db.prod.internal;Password=SuperSecretPass!123;
                User contact: ops-lead@aurora.io, +1 800 555 0199
                JWT: eyJhbGciOiJIUzI1NiJ9.eyJ1c2VyIjoiYWRtaW4ifQ.signature123456789
                """;

        String redacted = redactionService.redact(input);

        assertFalse(redacted.contains("SuperSecretPass!123"));
        assertFalse(redacted.contains("ops-lead@aurora.io"));
        assertFalse(redacted.contains("800 555 0199"));
        assertFalse(redacted.contains("eyJhbGciOiJIUzI1NiJ9"));
        assertTrue(redacted.contains("[REDACTED_CONNECTION_STRING]"));
        assertTrue(redacted.contains("[REDACTED_EMAIL]"));
        assertTrue(redacted.contains("[REDACTED_PHONE]"));
        assertTrue(redacted.contains("[REDACTED_JWT]"));
    }
}
