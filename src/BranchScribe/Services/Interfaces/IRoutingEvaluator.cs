namespace BranchScribe.Services.Interfaces;

using BranchScribe.Models;

public interface IRoutingEvaluator
{
    Task<RoutingDecision> EvaluateAsync(LlmRequest request, CancellationToken ct = default);
}

public interface IRoutingRule
{
    string Name { get; }
    Task<(InferenceTarget Preference, double Weight)> EvaluateAsync(LlmRequest request, CancellationToken ct = default);
}
