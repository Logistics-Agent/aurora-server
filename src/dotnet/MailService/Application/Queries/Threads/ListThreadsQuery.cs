using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;
using Shared.Security;
using MailService.Infrastructure.Persistence;
using MailService.Domain.Entities;
using MailService.Domain.Enums;

namespace MailService.Application.Queries.Threads;

public record ListThreadsQuery(
    Guid? MailboxId,
    int PageSize,
    string? PageToken,
    string? Scope = null,
    string? Status = null) : IRequest<List<EmailThread>>;

public class ListThreadsQueryHandler : IRequestHandler<ListThreadsQuery, List<EmailThread>>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ListThreadsQueryHandler(MailServiceDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<List<EmailThread>> Handle(ListThreadsQuery request, CancellationToken cancellationToken)
    {
        Guid tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required to list email threads.");

        bool hasSupervisoryAccess = _currentUserService.HasPermission(PermissionConstants.Mail.ThreadReadAll)
            || _currentUserService.HasPermission(PermissionConstants.Mail.ThreadReassign)
            || _currentUserService.HasPermission("mail:read_all")
            || _currentUserService.HasPermission("mail:assign");

        Guid? currentUserId = _currentUserService.UserId;

        var query = _dbContext.EmailThreads
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId);

        if (request.MailboxId.HasValue)
        {
            query = query.Where(t => t.MailboxId == request.MailboxId.Value);
        }

        // Scope filter enforcement
        string normalizedScope = (request.Scope ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizedScope == "UNASSIGNED")
        {
            query = query.Where(t => t.PrimaryAssigneeUserId == null);
        }
        else if (normalizedScope == "MY_WORK")
        {
            if (currentUserId.HasValue)
            {
                query = query.Where(t => t.PrimaryAssigneeUserId == currentUserId.Value);
            }
            else
            {
                query = query.Where(t => false);
            }
        }
        else if (normalizedScope == "ALL")
        {
            if (!hasSupervisoryAccess)
            {
                throw new UnauthorizedAccessException("FORBIDDEN_SCOPE_ALL: Only managers or users with supervisory mail permissions can query all threads.");
            }
        }
        else
        {
            // Default view: if supervisor -> all; if staff -> unassigned + my work
            if (!hasSupervisoryAccess && currentUserId.HasValue)
            {
                query = query.Where(t => t.PrimaryAssigneeUserId == null || t.PrimaryAssigneeUserId == currentUserId.Value);
            }
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ThreadStatus>(request.Status, true, out var threadStatus))
        {
            query = query.Where(t => t.Status == threadStatus);
        }

        int boundedPageSize = Math.Clamp(request.PageSize > 0 ? request.PageSize : 20, 1, 100);

        return await query
            .OrderByDescending(t => t.LastMessageAt)
            .Take(boundedPageSize)
            .ToListAsync(cancellationToken);
    }
}

