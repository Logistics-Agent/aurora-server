using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Rules;

public abstract class RuleEngineBase<TContext>(IEnumerable<IRule<TContext>> rules) : IRuleEngine<TContext>
{
    private readonly List<IRule<TContext>> _rules = rules.ToList();

    public virtual async Task<IReadOnlyList<RuleResult>> EvaluateAllAsync(TContext context, CancellationToken ct = default)
    {
        var results = new List<RuleResult>();

        foreach (var rule in _rules)
        {
            if (rule.CanApply(context))
            {
                var result = await rule.EvaluateAsync(context, ct);
                results.Add(result);
            }
        }

        return results;
    }
}
