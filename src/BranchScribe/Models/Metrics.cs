namespace BranchScribe.Models;

public class Metrics
{
    public double TimeSavedMinutes { get; set; }
    public double Co2PerInferenceGrams { get; set; }
    public double Co2SavedEstimateGrams { get; set; }
    public int CustomersHelped { get; set; }
    public double SentimentScore { get; set; } // -1.0 to 1.0
}
