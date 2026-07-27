using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using IamTenant.Application.Commands.Permissions;
using IamTenant.Application.Commands.Tenants;
using IamTenant.Application.Commands.Users;
using IamTenant.Application.Queries.Permissions;
using IamTenant.Application.Queries.Roles;
using IamTenant.Application.Queries.Tenants;
using MediatR;
using IamTenant.Grpc;
using Common.Grpc;
using Shared.Security;


namespace IamTenant.GrpcServices;

public class IamGrpcService(IMediator mediator, ICurrentUserService currentUser) : IamService.IamServiceBase
{
    // ═══════════════════════════════════════════════════════════════════════
    // TENANT RPCs
    // ═══════════════════════════════════════════════════════════════════════

    public override async Task<TenantResponse> CreateTenant(CreateTenantRequest request, ServerCallContext context)
    {
        try
        {
            var idempotencyKeyHeader = context.RequestHeaders.GetValue("idempotency-key");
            var idempotencyKey = Guid.TryParse(idempotencyKeyHeader, out var key) ? key : Guid.NewGuid();

            var dto = await mediator.Send(new CreateTenantCommand(
                request.Name,
                request.CompanyDomain,
                request.AdminEmail,
                idempotencyKey,
                PlanType: request.PlanType.ToString()), context.CancellationToken);

            return MapTenantResponse(dto, request.AdminEmail);
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<TenantResponse> GetTenant(GetTenantRequest request, ServerCallContext context)
    {
        try
        {
            Guid id;
            if (!Guid.TryParse(request.Id, out id))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid tenant ID."));

            var dto = await mediator.Send(new GetTenantQuery(id), context.CancellationToken);

            return MapTenantResponse(dto);
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    public override async Task<TenantResponse> UpdateTenantStatus(UpdateTenantStatusRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.TenantId, out var id))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid tenant ID."));

            // Map common.TenantStatus → domain status (command validate strict)
            var statusName = request.Status switch
            {
                Common.Grpc.TenantStatus.Active => "Active",
                Common.Grpc.TenantStatus.Suspended => "Suspended",
                _ => throw new RpcException(new Status(StatusCode.InvalidArgument, "Status phải là ACTIVE hoặc SUSPENDED."))
            };

            var dto = await mediator.Send(new UpdateTenantStatusCommand(id, statusName), context.CancellationToken);

            return MapTenantResponse(dto);
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<ListTenantsResponse> ListTenants(ListTenantsRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(new ListTenantsQuery
        {
            Page = request.Page,
            Limit = request.Limit
        }, context.CancellationToken);

        var response = new ListTenantsResponse
        {
            Page = result.Page,
            Limit = result.Limit,
            TotalItems = result.TotalItems,
            TotalPages = result.TotalPages
        };
        response.Tenants.AddRange(result.Items.Select(t => MapTenantResponse(t)));
        return response;
    }

    public override async Task<EmptyResponse> DeleteTenant(DeleteTenantRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.Id, out var id))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid tenant ID."));

            await mediator.Send(new DeleteTenantCommand(id), context.CancellationToken);
            return new EmptyResponse();
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // USER / STAFF RPCs
    // ═══════════════════════════════════════════════════════════════════════

    public override async Task<UserResponse> InviteUser(InviteUserRequest request, ServerCallContext context)
    {
        try
        {
            // InviteUser = CreateStaff — TenantId comes from ICurrentUserService (populated by AuthInterceptor)
            var dto = await mediator.Send(new CreateStaffCommand(
                Email: request.Email,
                FirstName: request.FirstName,
                LastName: request.LastName,
                StaffType: request.StaffType), context.CancellationToken);

            return MapUserResponse(dto);
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<UserResponse> GetUser(GetUserRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.Id, out var id))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID."));

            // TenantId lấy từ context người gọi (trước đây truyền Guid.Empty → không bao giờ match)
            var tenantId = currentUser.TenantId
                ?? throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context is missing."));

            var dto = await mediator.Send(new GetStaffQuery(id, tenantId), context.CancellationToken);

            return MapUserResponse(dto);
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    public override async Task<GetManyUsersResponse> GetManyUsers(GetManyUsersRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(new ListStaffQuery
        {
            Page = request.Page,
            Limit = request.Limit
        }, context.CancellationToken);

        var response = new GetManyUsersResponse
        {
            Page = result.Page,
            Limit = result.Limit,
            TotalItems = result.TotalItems,
            TotalPages = result.TotalPages
        };
        response.Users.AddRange(result.Items.Select(MapUserResponse));
        return response;
    }

    public override async Task<UserResponse> UpdateUser(UpdateUserRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.Id, out var id))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID."));

            var tenantId = currentUser.TenantId
                ?? throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context is missing."));

            var dto = await mediator.Send(new UpdateStaffCommand(
                id, tenantId, request.FirstName, request.LastName, request.StaffType), context.CancellationToken);

            return MapUserResponse(dto);
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    public override async Task<UserResponse> ActivateUser(ActivateUserRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID."));

            var tenantId = currentUser.TenantId
                ?? throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context is missing."));

            await mediator.Send(new ActivateStaffCommand(userId, tenantId), context.CancellationToken);
            var dto = await mediator.Send(new GetStaffQuery(userId, tenantId), context.CancellationToken);

            return MapUserResponse(dto);
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    public override async Task<EmptyResponse> ResetUserPassword(ResetUserPasswordRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID."));

            await mediator.Send(new ResetStaffPasswordCommand(userId), context.CancellationToken);
            return new EmptyResponse();
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    public override async Task<UserResponse> AssignRoles(AssignRolesRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID."));

            var roleIds = request.RoleIds.Select(id =>
                Guid.TryParse(id, out var g) ? g : throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid role ID: {id}"))).ToList();

            var dto = await mediator.Send(new AssignRolesCommand(userId, roleIds), context.CancellationToken);

            return MapUserResponse(dto);
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<UserResponse> SuspendUser(SuspendUserRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID."));

            var tenantId = currentUser.TenantId
                ?? throw new RpcException(new Status(StatusCode.PermissionDenied, "Tenant context is missing."));

            await mediator.Send(new DeactivateStaffCommand(userId, tenantId), context.CancellationToken);

            var dto = await mediator.Send(new GetStaffQuery(userId, tenantId), context.CancellationToken);

            return MapUserResponse(dto);
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ROLE RPCs — READ-ONLY (role mutations không được hỗ trợ theo thiết kế)
    // ═══════════════════════════════════════════════════════════════════════

    public override async Task<RoleResponse> GetRole(GetRoleRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.Id, out var id))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid role ID."));

            var dto = await mediator.Send(new GetRoleQuery(id), context.CancellationToken);
            return MapRoleResponse(dto);
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    public override async Task<GetManyRolesResponse> GetManyRoles(GetManyRolesRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(new ListRolesQuery
        {
            Page = request.Page,
            Limit = request.Limit
        }, context.CancellationToken);

        var response = new GetManyRolesResponse
        {
            Page = result.Page,
            Limit = result.Limit,
            TotalItems = result.TotalItems,
            TotalPages = result.TotalPages
        };
        response.Roles.AddRange(result.Items.Select(MapRoleResponse));
        return response;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PERMISSION RPCs
    // ═══════════════════════════════════════════════════════════════════════

    public override async Task<RoleResponse> AssignPermissionsToRole(AssignPermissionsToRoleRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.RoleId, out var roleId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid role ID."));

            var permIds = request.PermissionIds.Select(id =>
                Guid.TryParse(id, out var g) ? g : throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid permission ID: {id}"))).ToList();

            var dto = await mediator.Send(new AssignPermissionsToRoleCommand(roleId, permIds), context.CancellationToken);
            return MapRoleResponse(dto);
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<GetUserPermissionsResponse> GetUserPermissions(GetUserPermissionsRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID."));

            var dto = await mediator.Send(new GetUserPermissionsQuery(userId), context.CancellationToken);

            var response = new GetUserPermissionsResponse
            {
                UserId = dto.UserId.ToString()
            };
            response.RoleIds.AddRange(dto.RoleIds.Select(id => id.ToString()));
            response.Permissions.AddRange(dto.Permissions.Select(p => new PermissionInfo
            {
                Id = p.Id.ToString(),
                Code = p.Code,
                Module = p.Module
            }));
            return response;
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MAPPING HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private static TenantResponse MapTenantResponse(Application.DTOs.Tenants.TenantDto dto, string? adminEmail = null)
    {
        var response = new TenantResponse
        {
            Id = dto.Id.ToString(),
            Name = dto.Name,
            TenantCode = dto.Code,
            PlanType = MapPlanType(dto.PlanType),
            Status = MapTenantStatus(dto.Status),
            CreatedAt = Timestamp.FromDateTimeOffset(dto.CreatedAt)
        };
        if (!string.IsNullOrEmpty(adminEmail))
            response.AdminEmail = adminEmail;
        return response;
    }

    private static UserResponse MapUserResponse(Application.DTOs.Tenants.StaffDto dto) => new()
    {
        Id = dto.Id.ToString(),
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        Email = dto.Email,
        Status = MapUserStatus(dto.Status),
        StaffType = dto.StaffType,
        CreatedAt = Timestamp.FromDateTimeOffset(dto.CreatedAt)
    };

    private static PlanType MapPlanType(string planType) => planType.ToUpperInvariant() switch
    {
        "STANDARD" => PlanType.Standard,
        "ENTERPRISE" => PlanType.Enterprise,
        _ => PlanType.Unspecified
    };

    // So sánh KHÔNG phân biệt hoa/thường — DTO trả "Active" (enum ToString), không phải "ACTIVE"
    private static TenantStatus MapTenantStatus(string status) => status.ToUpperInvariant() switch
    {
        "TENANT_STATUS_ACTIVE" or "ACTIVE" => TenantStatus.Active,
        "TENANT_STATUS_SUSPENDED" or "SUSPENDED" => TenantStatus.Suspended,
        _ => TenantStatus.Unspecified
    };

    private static UserStatus MapUserStatus(string status) => status.ToUpperInvariant() switch
    {
        "USER_STATUS_ACTIVE" or "ACTIVE" => UserStatus.Active,
        "USER_STATUS_INACTIVE" or "INACTIVE" or "INVITED" => UserStatus.Inactive,
        "USER_STATUS_BLOCKED" or "BLOCKED" => UserStatus.Blocked,
        _ => UserStatus.Unspecified
    };

    private static RoleResponse MapRoleResponse(Application.DTOs.Roles.RoleDto dto)
    {
        var r = new RoleResponse
        {
            Id = dto.Id.ToString(),
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description ?? string.Empty,
            CreatedAt = Timestamp.FromDateTimeOffset(dto.CreatedAt)
        };
        r.PermissionIds.AddRange(dto.PermissionIds);
        return r;
    }
}
