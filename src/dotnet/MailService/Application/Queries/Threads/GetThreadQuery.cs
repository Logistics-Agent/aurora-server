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

public record GetThreadQuery(Guid ThreadId) : IRequest<ThreadDetailResult?>;

public record ThreadDetailResult(
    EmailThread Thread,
    List<ProcessedMessage> Messages,
    List<EmailDraft> Drafts,
    List<ThreadAssignmentHistory> AssignmentHistories);

public class GetThreadQueryHandler : IRequestHandler<GetThreadQuery, ThreadDetailResult?>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetThreadQueryHandler(MailServiceDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<ThreadDetailResult?> Handle(GetThreadQuery request, CancellationToken cancellationToken)
    {
        Guid tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required to query email threads.");

        var thread = await _dbContext.EmailThreads
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.ThreadId && t.TenantId == tenantId, cancellationToken);

        if (thread == null)
        {
            return null;
        }

        // Scope / Authorization enforcement
        var roleIds = _currentUserService.RoleIds ?? (IReadOnlyList<string>)Array.Empty<string>();
        var permissions = _currentUserService.Permissions ?? (IReadOnlyList<string>)Array.Empty<string>();
        bool hasSupervisoryAccess = _currentUserService.IsSystemAdmin()
            || _currentUserService.IsTenantAdmin()
            || roleIds.Any(r => string.Equals(r, RoleConstants.Manager, StringComparison.OrdinalIgnoreCase))
            || roleIds.Any(r => string.Equals(r, RoleConstants.TenantAdmin, StringComparison.OrdinalIgnoreCase))
            || permissions.Contains("mail:assign")
            || permissions.Contains("mail:read_all");

        if (!hasSupervisoryAccess && _currentUserService.UserId.HasValue)
        {
            Guid currentUserId = _currentUserService.UserId.Value;
            // Staff can read if unassigned or assigned to self
            if (thread.PrimaryAssigneeUserId.HasValue && thread.PrimaryAssigneeUserId.Value != currentUserId)
            {
                throw new UnauthorizedAccessException("THREAD_ASSIGNED_TO_ANOTHER_STAFF");
            }
        }

        var messages = await _dbContext.ProcessedMessages
            .AsNoTracking()
            .Where(m => m.ThreadId == request.ThreadId && m.TenantId == tenantId)
            .OrderBy(m => m.Direction == EmailDirection.Inbound ? m.ReceivedAt : m.ProcessedAt)
            .ToListAsync(cancellationToken);

        var drafts = await _dbContext.EmailDrafts
            .AsNoTracking()
            .Where(d => d.ThreadId == request.ThreadId && d.TenantId == tenantId && d.IsLatestRevision && d.Status == DraftStatus.Draft)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        var histories = await _dbContext.ThreadAssignmentHistories
            .AsNoTracking()
            .Where(h => h.ThreadId == request.ThreadId && h.TenantId == tenantId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

        return new ThreadDetailResult(thread, messages, drafts, histories);
    }
}

