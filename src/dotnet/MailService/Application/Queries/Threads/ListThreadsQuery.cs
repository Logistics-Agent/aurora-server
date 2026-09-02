using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;
using Shared.Security;
using MailService.Application.Common;
using MailService.Infrastructure.Persistence;
using MailService.Domain.Entities;
using MailService.Domain.Enums;

namespace MailService.Application.Queries.Threads;

public record ListThreadsQueryResult(
    List<EmailThread> Threads,
    string? NextPageToken,
    bool HasMore);

public record ListThreadsQuery(
    Guid? MailboxId,
    int PageSize,
    string? PageToken,
    string? Scope = null,
    string? Status = null,
    string? Search = null) : IRequest<ListThreadsQueryResult>;

public class ListThreadsQueryHandler : IRequestHandler<ListThreadsQuery, ListThreadsQueryResult>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ListThreadsQueryHandler(MailServiceDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<ListThreadsQueryResult> Handle(ListThreadsQuery request, CancellationToken cancellationToken)
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

        // Simple keyword search (Subject, Snippet)
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = $"%{request.Search.Trim()}%";

            if (_dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                string rawTerm = request.Search.Trim().ToLower();
                query = query.Where(t =>
                    (t.Subject != null && t.Subject.ToLower().Contains(rawTerm)) ||
                    (t.Snippet != null && t.Snippet.ToLower().Contains(rawTerm)));
            }
            else
            {
                query = query.Where(t =>
                    EF.Functions.ILike(t.Subject, term) ||
                    EF.Functions.ILike(t.Snippet ?? string.Empty, term));
            }
        }

        // Keyset Cursor Pagination
        if (!string.IsNullOrWhiteSpace(request.PageToken) && CursorHelper.TryDecode(request.PageToken, out var cursorDate, out var cursorId))
        {
            query = query.Where(t => t.LastMessageAt < cursorDate || (t.LastMessageAt == cursorDate && t.Id != cursorId));
        }

        // Stable ordering: LastMessageAt desc, Id desc
        query = query.OrderByDescending(t => t.LastMessageAt).ThenByDescending(t => t.Id);

        int boundedPageSize = Math.Clamp(request.PageSize > 0 ? request.PageSize : 20, 1, 100);

        // Fetch pageSize + 1 items to determine hasMore and generate nextPageToken
        var items = await query
            .Take(boundedPageSize + 1)
            .ToListAsync(cancellationToken);

        bool hasMore = items.Count > boundedPageSize;
        if (hasMore)
        {
            items = items.Take(boundedPageSize).ToList();
        }

        string? nextPageToken = null;
        if (hasMore && items.Count > 0)
        {
            var lastItem = items[^1];
            nextPageToken = CursorHelper.Encode(lastItem.LastMessageAt, lastItem.Id);
        }

        return new ListThreadsQueryResult(items, nextPageToken, hasMore);
    }
}
