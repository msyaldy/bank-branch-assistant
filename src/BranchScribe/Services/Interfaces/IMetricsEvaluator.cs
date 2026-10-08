namespace BranchScribe.Services.Interfaces;

using BranchScribe.Models;

public interface IMetricsEvaluator
{
    MetricsSnapshot RecordInference(double inferenceTimeMs, double latencyMs, double memoryUsageMb);
    Metrics CalculateSessionMetrics(Session session, int totalInferences);
    double CalculateCo2PerInference(double inferenceTimeSeconds);
    double CalculateTimeSaved(double baselineMinutes);
}
