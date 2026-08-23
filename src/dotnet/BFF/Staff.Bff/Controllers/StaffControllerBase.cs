using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StaffBff.Controllers;

/// <summary>
/// Base Controller dành cho các nghiệp vụ chung (Staff).
/// Prefix mặc định: /api/v{version}/[tên-controller] (v1 hiện tại).
/// Bắt buộc đăng nhập. Phân quyền chi tiết ở từng action bằng [RequirePermission].
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public abstract class StaffControllerBase : ControllerBase
{
}
