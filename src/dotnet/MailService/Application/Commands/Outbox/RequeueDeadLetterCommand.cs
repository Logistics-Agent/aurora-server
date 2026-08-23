using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;
using Shared.Events;
using Shared.Security;
using MailService.Application.Interfaces.Messaging;
using MailService.Domain.Entities;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Commands.Outbox;

public record RequeueDeadLetterCommand(Guid ProcessedMessageId) : IRequest<RequeueDeadLetterResult>;

public record RequeueDeadLetterResult(bool Success, string Message);

public class RequeueDeadLetterCommandHandler : IRequestHandler<RequeueDeadLetterCommand, RequeueDeadLetterResult>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly IOutboxWriter _outboxWriter;
    private readonly ICurrentUserService _currentUserService;

    public RequeueDeadLetterCommandHandler(
        MailServiceDbContext dbContext,
        IOutboxWriter outboxWriter,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _outboxWriter = outboxWriter;
        _currentUserService = currentUserService;
    }

    public async Task<RequeueDeadLetterResult> Handle(RequeueDeadLetterCommand request, CancellationToken cancellationToken)
    {
        bool isSystemAdmin = _currentUserService.IsSystemAdmin();

        ProcessedMessage? message;
        if (isSystemAdmin)
        {
            // Explicit cross-tenant access for verified System Admin role only
            message = await _dbContext.ProcessedMessages
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == request.ProcessedMessageId, cancellationToken);
        }
        else
        {
            Guid tenantId = _currentUserService.TenantId
                ?? throw new UnauthorizedAccessException("Tenant context is required to requeue dead letter message.");

            message = await _dbContext.ProcessedMessages
                .FirstOrDefaultAsync(m => m.Id == request.ProcessedMessageId, cancellationToken);
        }

        if (message == null)
        {
            throw new KeyNotFoundException($"Processed message with ID '{request.ProcessedMessageId}' not found.");
        }

        // Requeue event to Outbox using message's durable TenantId
        var evt = new InboundEmailReceivedEvent
        {
            TenantId = message.TenantId,
            MessageId = message.Id,
            SenderEmail = message.SenderAddress,
            RecipientEmails = message.RecipientAddresses,
            Subject = message.Subject ?? string.Empty,
            Classification = message.EmailCategory.ToString(),
            ReceivedAt = message.ReceivedAt.UtcDateTime
        };

        await _outboxWriter.WriteAsync(evt, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RequeueDeadLetterResult(true, $"Processed message '{request.ProcessedMessageId}' successfully requeued to outbox.");
    }
}
