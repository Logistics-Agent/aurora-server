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

public record ReassignThreadCommand(
    Guid ThreadId,
    Guid TargetUserId,
    string? Reason) : IRequest<ReassignThreadResult>;

public record ReassignThreadResult(
    bool Success,
    Guid ThreadId,
    Guid PrimaryAssigneeUserId,
    DateTimeOffset AssignedAt,
    string Status);

public class ReassignThreadCommandHandler : IRequestHandler<ReassignThreadCommand, ReassignThreadResult>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ReassignThreadCommandHandler(MailServiceDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<ReassignThreadResult> Handle(ReassignThreadCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required to reassign email threads.");
        Guid actorUserId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("Authenticated user ID is required to reassign email threads.");

        if (request.TargetUserId == Guid.Empty)
        {
            throw new ArgumentException("TargetUserId must be a valid non-empty GUID.");
        }

        var thread = await _dbContext.EmailThreads
            .FirstOrDefaultAsync(t => t.Id == request.ThreadId && t.TenantId == tenantId, cancellationToken);

        if (thread == null)
        {
            throw new KeyNotFoundException($"Thread {request.ThreadId} not found in tenant {tenantId}.");
        }

        var now = DateTimeOffset.UtcNow;
        var oldAssignee = thread.PrimaryAssigneeUserId;

        thread.PrimaryAssigneeUserId = request.TargetUserId;
        thread.AssignedAt = now;
        thread.Status = ThreadStatus.InProgress;
        thread.Version++;

        var history = new ThreadAssignmentHistory
        {
            ThreadId = thread.Id,
            TenantId = tenantId,
            FromUserId = oldAssignee,
            ToUserId = request.TargetUserId,
            Action = ThreadAssignmentAction.Reassigned,
            ActorUserId = actorUserId,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Manager reassigned thread" : request.Reason.Trim()
        };
        _dbContext.ThreadAssignmentHistories.Add(history);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ReassignThreadResult(true, thread.Id, request.TargetUserId, now, thread.Status.ToString());
    }
}
