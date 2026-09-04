package com.aurora.shared.security;

import java.util.Collections;
import java.util.List;
import java.util.UUID;

/**
 * Interface cung cấp thông tin người dùng hiện tại (matching C# ICurrentUserService).
 */
public interface ICurrentUserService {
    UUID getUserId();
    UUID getTenantId();
    String getTraceId();
    String getRole();

    List<String> getPermissions();
    Integer getPermissionVersion();
    boolean isSystemAdmin();
}
