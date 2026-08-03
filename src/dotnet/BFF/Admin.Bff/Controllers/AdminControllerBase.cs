using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminBff.Controllers;

/// <summary>
/// Base Controller dành cho Tenant Admin.
/// Prefix mặc định: /api/v{version}/admin/[tên-controller] (v1 hiện tại).
/// Chỉ có tài khoản mang role code TENANT_ADMIN mới có thể gọi các API này.
/// Quyền chi tiết sẽ được kiểm tra ở từng API bằng [RequirePermission].
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/[controller]")]
[Authorize(Roles = "TENANT_ADMIN")]
public abstract class AdminControllerBase : ControllerBase
{
}
