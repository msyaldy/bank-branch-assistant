namespace BranchScribe.Models;

public enum InferenceTarget { Local, Cloud }

public class RoutingDecision
{
    public InferenceTarget Target { get; set; } = InferenceTarget.Local;
    public string Reason { get; set; } = string.Empty;
    public Dictionary<string, double> EvaluatorScores { get; set; } = [];
}
