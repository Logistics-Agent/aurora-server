package com.aurora.aigovernance.gateway.domain.valueobject;

/**
 * Typed multimodal part for AI generation requests.
 */
public record MultimodalPart(
        PartType type,
        String text,
        FileReference file
) {
    public static MultimodalPart text(String text) {
        return new MultimodalPart(PartType.TEXT, text, null);
    }

    public static MultimodalPart file(String storageReference, String mimeType, String fileName, long sizeBytes) {
        return new MultimodalPart(PartType.FILE, null, new FileReference(storageReference, mimeType, fileName, sizeBytes));
    }

    public enum PartType {
        TEXT,
        FILE
    }

    public record FileReference(
            String storageReference,
            String mimeType,
            String fileName,
            long sizeBytes
    ) {}
}
