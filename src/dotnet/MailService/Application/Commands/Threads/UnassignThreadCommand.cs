using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Security;
using MailService.Infrastructure.Persistence;
using MailService.Domain.Entities;
using MailService.Domain.Enums;

namespace MailService.Application.Commands.Threads;

public record UnassignThreadCommand(
    Guid ThreadId,
    string? Reason) : IRequest<UnassignThreadResult>;

public record UnassignThreadResult(
    bool Success,
    Guid ThreadId,
    string Status);

public class UnassignThreadCommandHandler : IRequestHandler<UnassignThreadCommand, UnassignThreadResult>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public UnassignThreadCommandHandler(MailServiceDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<UnassignThreadResult> Handle(UnassignThreadCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required to unassign email threads.");
        Guid actorUserId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("Authenticated user ID is required to unassign email threads.");

        var thread = await _dbContext.EmailThreads
            .FirstOrDefaultAsync(t => t.Id == request.ThreadId && t.TenantId == tenantId, cancellationToken);

        if (thread == null)
        {
            throw new KeyNotFoundException($"Thread {request.ThreadId} not found in tenant {tenantId}.");
        }

        var oldAssignee = thread.PrimaryAssigneeUserId;

        thread.PrimaryAssigneeUserId = null;
        thread.AssignedAt = null;
        thread.Status = ThreadStatus.Unassigned;
        thread.Version++;

        var history = new ThreadAssignmentHistory
        {
            ThreadId = thread.Id,
            TenantId = tenantId,
            FromUserId = oldAssignee,
            ToUserId = null,
            Action = ThreadAssignmentAction.Unassigned,
            ActorUserId = actorUserId,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Thread unassigned and returned to pool" : request.Reason.Trim()
        };
        _dbContext.ThreadAssignmentHistories.Add(history);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UnassignThreadResult(true, thread.Id, thread.Status.ToString());
    }
}
