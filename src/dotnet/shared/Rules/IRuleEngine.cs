using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Rules;

public interface IRuleEngine<TContext>
{
    Task<IReadOnlyList<RuleResult>> EvaluateAllAsync(TContext context, CancellationToken ct = default);
}
