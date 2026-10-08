namespace BranchScribe.Services.Routing.Evaluators;

using BranchScribe.Models;
using BranchScribe.Services.Interfaces;

public class LatencyEvaluator : IRoutingRule
{
    public string Name => "Latency";

    public Task<(InferenceTarget Preference, double Weight)> EvaluateAsync(
        LlmRequest request, CancellationToken ct = default)
    {
        // During live sessions, local is preferred for low latency
        // Heuristic: if prompt is short (likely interactive), prefer local
        bool isInteractive = request.UserPrompt.Length < 500;
        return Task.FromResult(isInteractive
            ? (InferenceTarget.Local, 0.7)
            : (InferenceTarget.Local, 0.3));
    }
}
