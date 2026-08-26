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

public record ClaimThreadCommand(Guid ThreadId) : IRequest<ClaimThreadResult>;

public record ClaimThreadResult(
    bool Success,
    Guid ThreadId,
    Guid PrimaryAssigneeUserId,
    DateTimeOffset AssignedAt,
    string Status);

public class ClaimThreadCommandHandler : IRequestHandler<ClaimThreadCommand, ClaimThreadResult>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ClaimThreadCommandHandler(MailServiceDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<ClaimThreadResult> Handle(ClaimThreadCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required to claim email threads.");
        Guid userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("Authenticated user ID is required to claim email threads.");

        var thread = await _dbContext.EmailThreads
            .FirstOrDefaultAsync(t => t.Id == request.ThreadId && t.TenantId == tenantId, cancellationToken);

        if (thread == null)
        {
            throw new KeyNotFoundException($"Thread {request.ThreadId} not found in tenant {tenantId}.");
        }

        // Concurrency / duplicate claim check
        if (thread.PrimaryAssigneeUserId.HasValue)
        {
            if (thread.PrimaryAssigneeUserId.Value == userId)
            {
                return new ClaimThreadResult(true, thread.Id, userId, thread.AssignedAt ?? DateTimeOffset.UtcNow, thread.Status.ToString());
            }

            throw new InvalidOperationException("THREAD_ALREADY_ASSIGNED");
        }

        var now = DateTimeOffset.UtcNow;
        var oldAssignee = thread.PrimaryAssigneeUserId;

        thread.PrimaryAssigneeUserId = userId;
        thread.AssignedAt = now;
        thread.Status = ThreadStatus.InProgress;
        thread.Version++;

        var history = new ThreadAssignmentHistory
        {
            ThreadId = thread.Id,
            TenantId = tenantId,
            FromUserId = oldAssignee,
            ToUserId = userId,
            Action = ThreadAssignmentAction.Claimed,
            ActorUserId = userId,
            Reason = "Staff explicitly claimed unassigned thread"
        };
        _dbContext.ThreadAssignmentHistories.Add(history);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("THREAD_ALREADY_ASSIGNED");
        }

        return new ClaimThreadResult(true, thread.Id, userId, now, thread.Status.ToString());
    }
}
