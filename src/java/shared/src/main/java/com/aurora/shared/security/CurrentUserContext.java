package com.aurora.shared.security;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.UUID;

/**
 * ThreadLocal / Scoped context holder chứa identity của gRPC request hiện tại.
 */
public class CurrentUserContext implements ICurrentUserService {

    private static final ThreadLocal<CurrentUserContext> CONTEXT = ThreadLocal.withInitial(CurrentUserContext::new);

    private UUID userId;
    private UUID tenantId;
    private String traceId;
    private Integer permissionVersion;
    private String role;
    private List<String> permissions = new ArrayList<>();

    public static CurrentUserContext getCurrent() {
        return CONTEXT.get();
    }

    public static void setCurrent(CurrentUserContext context) {
        CONTEXT.set(context);
    }

    public static void clear() {
        CONTEXT.remove();
    }

    public void populate(UUID userId, UUID tenantId, String traceId, Integer permissionVersion,
                         String role, List<String> permissions) {
        this.userId = userId;
        this.tenantId = tenantId;
        this.traceId = traceId;
        this.permissionVersion = permissionVersion;
        this.role = role;
        this.permissions = permissions != null ? new ArrayList<>(permissions) : new ArrayList<>();
    }

    @Override
    public UUID getUserId() {
        return userId;
    }

    @Override
    public UUID getTenantId() {
        return tenantId;
    }

    @Override
    public String getTraceId() {
        return traceId;
    }

    @Override
    public Integer getPermissionVersion() {
        return permissionVersion;
    }

    @Override
    public String getRole() {
        return role;
    }

    @Override
    public List<String> getPermissions() {
        return Collections.unmodifiableList(permissions);
    }

    @Override
    public boolean isSystemAdmin() {
        return "SYSTEM_ADMIN".equalsIgnoreCase(role);
    }
}
