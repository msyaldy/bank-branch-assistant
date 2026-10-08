namespace BranchScribe.Services.Routing;

using BranchScribe.Models;
using BranchScribe.Services.Interfaces;

public class HybridRoutingEvaluator : IRoutingEvaluator
{
    private readonly IEnumerable<IRoutingRule> _rules;

    public HybridRoutingEvaluator(IEnumerable<IRoutingRule> rules)
    {
        _rules = rules;
    }

    public async Task<RoutingDecision> EvaluateAsync(LlmRequest request, CancellationToken ct = default)
    {
        var decision = new RoutingDecision();
        double localScore = 0;
        double cloudScore = 0;

        foreach (var rule in _rules)
        {
            var (preference, weight) = await rule.EvaluateAsync(request, ct);
            decision.EvaluatorScores[rule.Name] = weight;

            if (preference == InferenceTarget.Local)
                localScore += weight;
            else
                cloudScore += weight;
        }

        decision.Target = localScore >= cloudScore ? InferenceTarget.Local : InferenceTarget.Cloud;
        decision.Reason = $"Local={localScore:F2}, Cloud={cloudScore:F2}. " +
                          string.Join("; ", decision.EvaluatorScores.Select(kv => $"{kv.Key}={kv.Value:F2}"));

        return decision;
    }
}
