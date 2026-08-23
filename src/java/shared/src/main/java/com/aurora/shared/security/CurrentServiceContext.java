package com.aurora.shared.security;

/**
 * ThreadLocal / Scoped context holder chứa identity của caller service/workload.
 * <p>
 * Tách biệt hoàn toàn khỏi {@link CurrentUserContext} — đây là hai khái niệm ngữ nghĩa độc lập:
 * <ul>
 *   <li>{@code CurrentUserContext} = human/user identity (có thể null cho internal automation)</li>
 *   <li>{@code CurrentServiceContext} = immediate caller workload identity</li>
 * </ul>
 * <p>
 * {@code x-service-id} đại diện cho <b>immediate authenticated caller workload</b>,
 * không phải originating service. Full trace chain thuộc OpenTelemetry.
 * <p>
 * ThreadLocal chỉ tồn tại ở transport boundary (gRPC interceptor → handler).
 * Application logic phải copy {@code serviceId} vào command fields và không dùng ThreadLocal trực tiếp.
 */
public class CurrentServiceContext {

    private static final ThreadLocal<CurrentServiceContext> CONTEXT =
            ThreadLocal.withInitial(CurrentServiceContext::new);

    private String serviceId;

    public static CurrentServiceContext getCurrent() {
        return CONTEXT.get();
    }

    public static void setCurrent(CurrentServiceContext context) {
        CONTEXT.set(context);
    }

    public static void clear() {
        CONTEXT.remove();
    }

    public void populate(String serviceId) {
        this.serviceId = serviceId;
    }

    /**
     * Returns the immediate caller workload/service identity.
     * May be null if no {@code x-service-id} header was provided.
     */
    public String getServiceId() {
        return serviceId;
    }
}
