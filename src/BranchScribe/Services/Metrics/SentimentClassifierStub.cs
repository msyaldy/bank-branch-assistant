namespace BranchScribe.Services.Metrics;

using BranchScribe.Services.Interfaces;

/// <summary>
/// Stub sentiment classifier. Returns a simple keyword-based score.
/// TODO: Replace with actual model-based sentiment classification.
/// </summary>
public class SentimentClassifierStub : ISentimentClassifier
{
    private static readonly string[] PositiveWords = ["great", "good", "thank", "excellent", "happy", "pleased", "wonderful", "perfect", "congratulations"];
    private static readonly string[] NegativeWords = ["bad", "problem", "issue", "difficult", "worry", "concern", "frustrated", "confused", "unfortunately"];

    public Task<double> ClassifyAsync(string text, CancellationToken ct = default)
    {
        var lower = text.ToLowerInvariant();
        int positive = PositiveWords.Count(w => lower.Contains(w));
        int negative = NegativeWords.Count(w => lower.Contains(w));
        int total = positive + negative;

        double score = total == 0 ? 0.0 : (positive - negative) / (double)total;
        return Task.FromResult(Math.Clamp(score, -1.0, 1.0));
    }
}
