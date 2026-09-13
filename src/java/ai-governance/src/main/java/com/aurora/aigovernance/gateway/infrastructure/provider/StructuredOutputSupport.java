package com.aurora.aigovernance.gateway.infrastructure.provider;

import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateRequest;

/**
 * Provider-neutral structured-output contract for capabilities that are parsed by a strict consumer.
 */
public final class StructuredOutputSupport {

    public static final String JSON_OBJECT_RESPONSE_FORMAT = "json_object";

    private StructuredOutputSupport() {
    }

    public static boolean requiresJsonObject(AiGenerateRequest request) {
        return "compliance.answer".equals(request.capabilityCode())
                || JSON_OBJECT_RESPONSE_FORMAT.equalsIgnoreCase(
                request.parameters() == null ? null : request.parameters().get("response_format"));
    }

    public static String deterministicJsonResponse() {
        return "{\"answer\":\"The configured test AI provider returned no generated answer.\","
                + "\"citations\":[],"
                + "\"knowledgeReferences\":[],"
                + "\"conflicts\":[],"
                + "\"insufficientEvidence\":true,"
                + "\"missingInformation\":[\"A live AI provider is required to generate a grounded answer.\"]}";
    }
}
