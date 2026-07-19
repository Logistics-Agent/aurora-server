using System.Threading;
using System.Threading.Tasks;

namespace Shared.Rules;

public interface IRule<TContext>
{
    string Name { get; }
    bool CanApply(TContext context);
    Task<RuleResult> EvaluateAsync(TContext context, CancellationToken ct = default);
}
