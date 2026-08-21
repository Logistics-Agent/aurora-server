using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SystemBff.Controllers;

/// <summary>
/// Base Controller dành cho System Admin.
/// Prefix mặc định: /api/v{version}/system/[tên-controller] (v1 hiện tại).
/// Chỉ có tài khoản mang role code SYSTEM_ADMIN mới có thể gọi các API này.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system/[controller]")]
[Authorize(Roles = "SYSTEM_ADMIN")]
public abstract class SystemControllerBase : ControllerBase
{
}
