namespace BranchScribe.Services.Routing.Evaluators;

using BranchScribe.Models;
using BranchScribe.Services.Interfaces;

public class CostEvaluator : IRoutingRule
{
    public string Name => "Cost";

    public Task<(InferenceTarget Preference, double Weight)> EvaluateAsync(
        LlmRequest request, CancellationToken ct = default)
    {
        // Local is always cheaper (no per-token cost)
        // Placeholder: in production, compare estimated token costs
        return Task.FromResult((InferenceTarget.Local, 0.5));
    }
}
