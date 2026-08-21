package com.aurora.devopsagent.Infrastructure.Security;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * RedactionService: Sanitizes raw logs, incident contexts, and exception traces
 * before passing them to RAG or AiGovernance LLM execution.
 */
@Service
public class RedactionService {

    private static final Logger log = LoggerFactory.getLogger(RedactionService.class);

    // 1. JWT Tokens
    private static final Pattern JWT_PATTERN = Pattern.compile(
            "eyJ[A-Za-z0-9-_]+\\.[A-Za-z0-9-_]+\\.[A-Za-z0-9-_]+");

    // 2. Bearer Authorization Headers
    private static final Pattern BEARER_PATTERN = Pattern.compile(
            "Bearer\\s+[A-Za-z0-9-_\\.]+", Pattern.CASE_INSENSITIVE);

    // 3. Database Connection Strings / Credentials
    private static final Pattern CONN_STRING_PATTERN = Pattern.compile(
            "(?i)(?:Server|Host|Data Source|User Id|Password|Database|Uid|Pwd|mongodb(\\+srv)?://|postgres://|mysql://|jdbc:[a-z:]+)[:=][^;\\r\\n\\s]+");

    // 4. API Keys (Gemini, OpenAI, Azure, AWS, Generic)
    private static final Pattern GEMINI_KEY_PATTERN = Pattern.compile("AIzaSy[A-Za-z0-9_-]{33}");
    private static final Pattern AWS_KEY_PATTERN = Pattern.compile("AKIA[0-9A-Z]{16}");
    private static final Pattern GENERIC_KEY_PATTERN = Pattern.compile(
            "(?i)(?:api[_-]?key|secret[_-]?key|access[_-]?token|auth[_-]?token)\\s*[:=]\\s*['\"]?[A-Za-z0-9_\\-]{16,}['\"]?");

    // 5. PEM Private Key Blocks
    private static final Pattern PEM_PRIVATE_KEY_PATTERN = Pattern.compile(
            "-----BEGIN (?:[A-Z ]*)?PRIVATE KEY-----[\\s\\S]*?-----END (?:[A-Z ]*)?PRIVATE KEY-----");

    // 6. Passwords in config/JSON
    private static final Pattern PASSWORD_PATTERN = Pattern.compile(
            "(?i)(?:password|passwd|pwd|secret)\\s*[:=]\\s*['\"]([^'\"]+)['\"]");

    // 7. PII: Emails and Phone numbers
    private static final Pattern EMAIL_PATTERN = Pattern.compile(
            "[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}");
    private static final Pattern PHONE_PATTERN = Pattern.compile(
            "(?:\\+?\\d{1,3}[-.\\s]?)?\\(?\\d{3}\\)?[-.\\s]?\\d{3}[-.\\s]?\\d{4}");

    /**
     * Sanitizes input text by removing all detected secrets, tokens, credentials, and PII.
     */
    public String redact(String input) {
        if (input == null || input.isBlank()) {
            return input;
        }

        String result = input;

        // PEM Private Keys
        result = PEM_PRIVATE_KEY_PATTERN.matcher(result).replaceAll("[REDACTED_PRIVATE_KEY]");

        // JWT
        result = JWT_PATTERN.matcher(result).replaceAll("[REDACTED_JWT]");

        // Bearer
        result = BEARER_PATTERN.matcher(result).replaceAll("Bearer [REDACTED_TOKEN]");

        // Connection Strings
        result = CONN_STRING_PATTERN.matcher(result).replaceAll("[REDACTED_CONNECTION_STRING]");

        // Gemini API Keys
        result = GEMINI_KEY_PATTERN.matcher(result).replaceAll("[REDACTED_GEMINI_KEY]");

        // AWS API Keys
        result = AWS_KEY_PATTERN.matcher(result).replaceAll("[REDACTED_AWS_KEY]");

        // Generic API Keys
        result = GENERIC_KEY_PATTERN.matcher(result).replaceAll("[REDACTED_API_KEY]");

        // Passwords
        result = PASSWORD_PATTERN.matcher(result).replaceAll("password=\"[REDACTED_PASSWORD]\"");

        // PII
        result = EMAIL_PATTERN.matcher(result).replaceAll("[REDACTED_EMAIL]");
        result = PHONE_PATTERN.matcher(result).replaceAll("[REDACTED_PHONE]");

        return result;
    }
}
