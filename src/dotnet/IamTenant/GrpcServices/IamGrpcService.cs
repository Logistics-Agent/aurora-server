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
                PlanType: MapPlanTypeToDomain(request.PlanType)), context.CancellationToken);

            return new TenantResponse
            {
                Id = dto.Id.ToString(),
                Name = dto.Name,
                TenantCode = dto.Code,
                PlanType = MapPlanTypeToGrpc(dto.PlanType),
                Status = MapTenantStatusToGrpc(dto.Status),
                AdminEmail = request.AdminEmail,
                CreatedAt = Timestamp.FromDateTimeOffset(dto.CreatedAt)
            };
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

            return new TenantResponse
            {
                Id = dto.Id.ToString(),
                Name = dto.Name,
                TenantCode = dto.Code,
                PlanType = MapPlanTypeToGrpc(dto.PlanType),
                Status = MapTenantStatusToGrpc(dto.Status),
                CreatedAt = Timestamp.FromDateTimeOffset(dto.CreatedAt)
            };
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

            var dto = await mediator.Send(new UpdateTenantStatusCommand(
                id,
                request.Status.ToString()), context.CancellationToken);

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
            var roleGuids = request.RoleIds
                .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .ToList();

            var dto = await mediator.Send(new CreateStaffCommand(
                Email: request.Email,
                FirstName: request.FirstName,
                LastName: request.LastName,
                RoleIds: roleGuids), context.CancellationToken);

            var userResponse = new UserResponse
            {
                Id = dto.Id.ToString(),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Status = MapUserStatusToGrpc(dto.Status)
            };
            userResponse.RoleIds.AddRange(request.RoleIds);
            return userResponse;
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

            return new UserResponse
            {
                Id = dto.Id.ToString(),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Status = MapUserStatusToGrpc(dto.Status)
            };
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
        response.Users.AddRange(result.Items.Select(u => new UserResponse
        {
            Id = u.Id.ToString(),
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.Email,
            Status = MapUserStatusToGrpc(u.Status)
        }));
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

            var userResponse = new UserResponse
            {
                Id = dto.Id.ToString(),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Status = MapUserStatusToGrpc(dto.Status)
            };
            userResponse.RoleIds.AddRange(request.RoleIds);
            return userResponse;
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

            var dto = await mediator.Send(new GetStaffQuery(userId, tenantId), context.CancellationToken);

            await mediator.Send(new DeactivateStaffCommand(userId, tenantId), context.CancellationToken);

            return MapUserResponse(dto);
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ROLE RPCs (Read-Only as specified: No Write operations)
    // ═══════════════════════════════════════════════════════════════════════

    public override Task<RoleResponse> CreateCustomRole(CreateCustomRoleRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Role is Read-Only. Custom Role creation is disabled."));
    }

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

    public override Task<RoleResponse> UpdateRole(UpdateRoleRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Role is Read-Only. Custom Role update is disabled."));
    }

    public override Task<EmptyResponse> DeleteRole(DeleteRoleRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Role is Read-Only. Custom Role deletion is disabled."));
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

    private static Common.Grpc.PlanType MapPlanTypeToGrpc(IamTenant.Domain.Enums.PlanType planType) => planType switch
    {
        IamTenant.Domain.Enums.PlanType.Standard => Common.Grpc.PlanType.Standard,
        IamTenant.Domain.Enums.PlanType.Enterprise => Common.Grpc.PlanType.Enterprise,
        _ => Common.Grpc.PlanType.Unspecified
    };

    private static IamTenant.Domain.Enums.PlanType MapPlanTypeToDomain(Common.Grpc.PlanType planType) => planType switch
    {
        Common.Grpc.PlanType.Standard => IamTenant.Domain.Enums.PlanType.Standard,
        Common.Grpc.PlanType.Enterprise => IamTenant.Domain.Enums.PlanType.Enterprise,
        _ => IamTenant.Domain.Enums.PlanType.Standard
    };

    private static Common.Grpc.TenantStatus MapTenantStatusToGrpc(IamTenant.Domain.Enums.TenantStatus status) => status switch
    {
        IamTenant.Domain.Enums.TenantStatus.Active => Common.Grpc.TenantStatus.Active,
        IamTenant.Domain.Enums.TenantStatus.Suspended => Common.Grpc.TenantStatus.Suspended,
        _ => Common.Grpc.TenantStatus.Unspecified
    };

    private static IamTenant.Domain.Enums.TenantStatus MapTenantStatusToDomain(Common.Grpc.TenantStatus status) => status switch
    {
        Common.Grpc.TenantStatus.Active => IamTenant.Domain.Enums.TenantStatus.Active,
        Common.Grpc.TenantStatus.Suspended => IamTenant.Domain.Enums.TenantStatus.Suspended,
        _ => IamTenant.Domain.Enums.TenantStatus.Provisioning
    };

    private static Common.Grpc.UserStatus MapUserStatusToGrpc(IamTenant.Domain.Enums.UserStatus status) => status switch
    {
        IamTenant.Domain.Enums.UserStatus.Active => Common.Grpc.UserStatus.Active,
        IamTenant.Domain.Enums.UserStatus.Blocked => Common.Grpc.UserStatus.Blocked,
        IamTenant.Domain.Enums.UserStatus.Invited => Common.Grpc.UserStatus.Unspecified,
        _ => Common.Grpc.UserStatus.Unspecified
    };

    private static IamTenant.Domain.Enums.UserStatus MapUserStatusToDomain(Common.Grpc.UserStatus status) => status switch
    {
        Common.Grpc.UserStatus.Active => IamTenant.Domain.Enums.UserStatus.Active,
        Common.Grpc.UserStatus.Blocked => IamTenant.Domain.Enums.UserStatus.Blocked,
        Common.Grpc.UserStatus.Inactive => IamTenant.Domain.Enums.UserStatus.Invited,
        _ => IamTenant.Domain.Enums.UserStatus.Invited
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

    private static TenantResponse MapTenantResponse(Application.DTOs.Tenants.TenantDto dto)
    {
        return new TenantResponse
        {
            Id = dto.Id.ToString(),
            Name = dto.Name,
            TenantCode = dto.Code,
            PlanType = MapPlanTypeToGrpc(dto.PlanType),
            Status = MapTenantStatusToGrpc(dto.Status),
            CreatedAt = Timestamp.FromDateTimeOffset(dto.CreatedAt)
        };
    }

    private static UserResponse MapUserResponse(Application.DTOs.Tenants.StaffDto dto)
    {
        return new UserResponse
        {
            Id = dto.Id.ToString(),
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Status = MapUserStatusToGrpc(dto.Status)
        };
    }
}
