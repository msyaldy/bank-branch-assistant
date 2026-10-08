namespace BranchScribe.Services.Routing.Evaluators;

using BranchScribe.Models;
using BranchScribe.Services.Interfaces;
using System.Text.RegularExpressions;

public partial class PrivacyEvaluator : IRoutingRule
{
    public string Name => "Privacy";

    public Task<(InferenceTarget Preference, double Weight)> EvaluateAsync(
        LlmRequest request, CancellationToken ct = default)
    {
        var content = $"{request.SystemPrompt} {request.UserPrompt}";

        // Detect potentially sensitive content: long digit strings (card numbers, NI numbers etc.)
        bool hasSensitiveDigits = LongDigitPattern().IsMatch(content);
        // Detect common PII patterns
        bool hasPiiKeywords = content.Contains("national insurance", StringComparison.OrdinalIgnoreCase) ||
                              content.Contains("sort code", StringComparison.OrdinalIgnoreCase) ||
                              content.Contains("account number", StringComparison.OrdinalIgnoreCase) ||
                              content.Contains("passport", StringComparison.OrdinalIgnoreCase);

        if (hasSensitiveDigits || hasPiiKeywords)
            return Task.FromResult((InferenceTarget.Local, 1.0));

        return Task.FromResult((InferenceTarget.Local, 0.3));
    }

    [GeneratedRegex(@"\b\d{6,}\b")]
    private static partial Regex LongDigitPattern();
}
