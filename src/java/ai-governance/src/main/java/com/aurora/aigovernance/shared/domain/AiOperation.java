package com.aurora.aigovernance.shared.domain;

/**
 * AI operation type. Used for:
 * <ul>
 *   <li>Provider slot compatibility filtering</li>
 *   <li>Provider routing</li>
 *   <li>Capacity accounting</li>
 *   <li>Metrics labels</li>
 *   <li>Structured logs</li>
 *   <li>Future operation-specific policies</li>
 * </ul>
 * <p>
 * Domain/application code uses this enum — string serialization ("generate"/"embed")
 * only at observability boundaries.
 */
public enum AiOperation {
    GENERATE,
    EMBED
}
