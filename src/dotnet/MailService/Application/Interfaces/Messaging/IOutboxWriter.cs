using System.Threading;
using System.Threading.Tasks;

namespace MailService.Application.Interfaces.Messaging;

public interface IOutboxWriter
{
    Task WriteAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class;
}
