namespace BranchScribe.Services.Interfaces;

public interface ISentimentClassifier
{
    Task<double> ClassifyAsync(string text, CancellationToken ct = default);
}
