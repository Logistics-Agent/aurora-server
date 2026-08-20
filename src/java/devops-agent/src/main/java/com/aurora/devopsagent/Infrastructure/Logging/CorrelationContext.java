package com.aurora.devopsagent.Infrastructure.Logging;

import org.slf4j.MDC;

import java.util.HashMap;
import java.util.Map;

/**
 * CorrelationContext: Utility for propagating correlation_id, incident_id, tenant_id, trace_id
 * through SLF4J MDC across async workers, gRPC calls, and event streams.
 * Includes AutoCloseable scope to guarantee MDC cleanup and prevent context leaks.
 */
public final class CorrelationContext {

    public static final String CORRELATION_ID = "correlationId";
    public static final String INCIDENT_ID = "incidentId";
    public static final String TENANT_ID = "tenantId";
    public static final String TRACE_ID = "traceId";

    private CorrelationContext() {}

    public static void set(String correlationId, String incidentId, String tenantId, String traceId) {
        if (correlationId != null) MDC.put(CORRELATION_ID, correlationId);
        if (incidentId != null) MDC.put(INCIDENT_ID, incidentId);
        if (tenantId != null) MDC.put(TENANT_ID, tenantId);
        if (traceId != null) MDC.put(TRACE_ID, traceId);
    }

    public static void setCorrelationId(String correlationId) {
        if (correlationId != null) MDC.put(CORRELATION_ID, correlationId);
    }

    public static String getCorrelationId() {
        return MDC.get(CORRELATION_ID);
    }

    public static void clear() {
        MDC.remove(CORRELATION_ID);
        MDC.remove(INCIDENT_ID);
        MDC.remove(TENANT_ID);
        MDC.remove(TRACE_ID);
    }

    public static Map<String, String> capture() {
        Map<String, String> context = new HashMap<>();
        String corr = MDC.get(CORRELATION_ID);
        String inc = MDC.get(INCIDENT_ID);
        String ten = MDC.get(TENANT_ID);
        String trc = MDC.get(TRACE_ID);
        if (corr != null) context.put(CORRELATION_ID, corr);
        if (inc != null) context.put(INCIDENT_ID, inc);
        if (ten != null) context.put(TENANT_ID, ten);
        if (trc != null) context.put(TRACE_ID, trc);
        return context;
    }

    public static void restore(Map<String, String> context) {
        if (context != null) {
            context.forEach(MDC::put);
        }
    }

    /**
     * AutoCloseable scope for try-with-resources.
     */
    public static Scope withScope(String correlationId, String incidentId, String tenantId, String traceId) {
        set(correlationId, incidentId, tenantId, traceId);
        return new Scope();
    }

    public static class Scope implements AutoCloseable {
        @Override
        public void close() {
            clear();
        }
    }
}
