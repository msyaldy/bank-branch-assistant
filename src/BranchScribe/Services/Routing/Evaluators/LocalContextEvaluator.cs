namespace BranchScribe.Services.Routing.Evaluators;

using BranchScribe.Models;
using BranchScribe.Services.Interfaces;

public class LocalContextEvaluator : IRoutingRule
{
    public string Name => "LocalContext";

    public Task<(InferenceTarget Preference, double Weight)> EvaluateAsync(
        LlmRequest request, CancellationToken ct = default)
    {
        // If the prompt references local knowledge base content, prefer local
        // TODO: Check if a local knowledge base / RAG index is available
        bool hasLocalKnowledgeBase = false; // placeholder

        return Task.FromResult(hasLocalKnowledgeBase
            ? (InferenceTarget.Local, 0.6)
            : (InferenceTarget.Cloud, 0.2));
    }
}
