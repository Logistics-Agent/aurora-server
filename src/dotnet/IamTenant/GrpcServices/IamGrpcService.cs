using System;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using IamTenant.Application.Commands.Permissions;
using IamTenant.Application.Commands.Tenants;
using IamTenant.Application.Commands.Users;
using IamTenant.Application.Queries.Permissions;
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
            if (!Guid.TryParse(request.Id, out var id))
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
        response.Tenants.AddRange(result.Items.Select(MapTenantResponse));
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
            var dto = await mediator.Send(new CreateStaffCommand(
                Email: request.Email,
                FirstName: request.FirstName,
                LastName: request.LastName,
                Role: string.IsNullOrWhiteSpace(request.Role) ? "STAFF" : request.Role,
                ApplyDefaultPermissions: request.ApplyDefaultPermissions,
                Permissions: request.Permissions.ToList()), context.CancellationToken);

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
                id, tenantId, request.FirstName, request.LastName), context.CancellationToken);

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
    // USER BASE ROLE UPDATE (Single Persona Role)
    // ═══════════════════════════════════════════════════════════════════════

    public override async Task<UserRoleResponse> UpdateUserRole(UpdateUserRoleRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID."));

            var result = await mediator.Send(new UpdateUserRoleCommand(
                userId, request.NewRole, request.ApplyDefaultPermissions), context.CancellationToken);

            var response = new UserRoleResponse
            {
                UserId = result.UserId.ToString(),
                Role = result.Role,
                PermissionVersion = result.PermissionVersion
            };
            response.Permissions.AddRange(result.Permissions);
            response.ElevatedPermissionsRetained.AddRange(result.ElevatedPermissionsRetained);
            return response;
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DIRECT USER PERMISSION RPCs (Delta Grant / Revoke)
    // ═══════════════════════════════════════════════════════════════════════

    public override async Task<UserPermissionsResponse> UpdateUserPermissions(UpdateUserPermissionsRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID."));

            var dto = await mediator.Send(new UpdateUserPermissionsCommand(
                userId, request.Grant.ToList(), request.Revoke.ToList()), context.CancellationToken);

            var response = new UserPermissionsResponse
            {
                UserId = dto.UserId.ToString(),
                Role = dto.Role,
                PermissionVersion = dto.Version
            };
            response.Permissions.AddRange(dto.Permissions.Select(p => p.Code));
            return response;
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<BulkUpdateUserPermissionsResponse> BulkUpdateUserPermissions(BulkUpdateUserPermissionsRequest request, ServerCallContext context)
    {
        try
        {
            var userGuids = request.UserIds
                .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .ToList();

            var result = await mediator.Send(new BulkUpdateUserPermissionsCommand(
                userGuids, request.Grant.ToList(), request.Revoke.ToList()), context.CancellationToken);

            var response = new BulkUpdateUserPermissionsResponse
            {
                UpdatedUsersCount = result.UpdatedUsersCount
            };
            response.AffectedUserIds.AddRange(result.AffectedUserIds.Select(id => id.ToString()));
            return response;
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
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
                UserId = dto.UserId.ToString(),
                Role = dto.Role,
                PermissionVersion = dto.Version
            };
            response.Permissions.AddRange(dto.Permissions.Select(p => p.Code));
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

    private static Common.Grpc.UserStatus MapUserStatusToGrpc(IamTenant.Domain.Enums.UserStatus status) => status switch
    {
        IamTenant.Domain.Enums.UserStatus.Active => Common.Grpc.UserStatus.Active,
        IamTenant.Domain.Enums.UserStatus.Blocked => Common.Grpc.UserStatus.Blocked,
        IamTenant.Domain.Enums.UserStatus.Invited => Common.Grpc.UserStatus.Unspecified,
        _ => Common.Grpc.UserStatus.Unspecified
    };

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
        var r = new UserResponse
        {
            Id = dto.Id.ToString(),
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Status = MapUserStatusToGrpc(dto.Status),
            Role = dto.Role,
            PermissionVersion = dto.PermissionVersion,
            TenantId = dto.TenantId.ToString(),
            CreatedAt = Timestamp.FromDateTimeOffset(dto.CreatedAt)
        };
        r.Permissions.AddRange(dto.Permissions);
        return r;
    }
}
