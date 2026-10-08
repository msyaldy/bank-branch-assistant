namespace BranchScribe.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using BranchScribe.Models;
using System.Collections.ObjectModel;

public partial class InsightsViewModel : ObservableObject
{
    [ObservableProperty]
    private double _totalTimeSavedMinutes;

    public string TotalTimeSavedDisplay => $"{TotalTimeSavedMinutes:F2}";

    partial void OnTotalTimeSavedMinutesChanged(double value) =>
        OnPropertyChanged(nameof(TotalTimeSavedDisplay));

    [ObservableProperty]
    private double _totalCo2SavedGrams;

    public string TotalCo2SavedDisplay => $"{TotalCo2SavedGrams:F2}";

    partial void OnTotalCo2SavedGramsChanged(double value) =>
        OnPropertyChanged(nameof(TotalCo2SavedDisplay));

    [ObservableProperty]
    private double _co2PerInferenceGrams;

    public string Co2PerInferenceDisplay => $"{Co2PerInferenceGrams:F2}";

    partial void OnCo2PerInferenceGramsChanged(double value) =>
        OnPropertyChanged(nameof(Co2PerInferenceDisplay));

    [ObservableProperty]
    private int _totalCustomersHelped;

    [ObservableProperty]
    private double _averageSentiment;

    public string AverageSentimentDisplay => AverageSentiment >= 0
        ? $"+{AverageSentiment:F2}"
        : $"{AverageSentiment:F2}";

    partial void OnAverageSentimentChanged(double value) =>
        OnPropertyChanged(nameof(AverageSentimentDisplay));

    [ObservableProperty]
    private string _plannerSuggestions = string.Empty;

    public ObservableCollection<SessionSummary> SessionHistory { get; } = [];

    public void AddSessionMetrics(Session session)
    {
        TotalTimeSavedMinutes += session.Metrics.TimeSavedMinutes;
        TotalCo2SavedGrams += session.Metrics.Co2SavedEstimateGrams;
        Co2PerInferenceGrams = session.Metrics.Co2PerInferenceGrams;
        TotalCustomersHelped += session.Metrics.CustomersHelped;

        SessionHistory.Add(new SessionSummary
        {
            SessionId = session.Id,
            CustomerAlias = session.CustomerAlias,
            StartedAt = session.StartedAt,
            TimeSavedMinutes = session.Metrics.TimeSavedMinutes,
            SentimentScore = session.Metrics.SentimentScore,
        });

        // Recalculate average sentiment
        if (SessionHistory.Count > 0)
            AverageSentiment = SessionHistory.Average(s => s.SentimentScore);

        UpdatePlannerSuggestions();
    }

    private void UpdatePlannerSuggestions()
    {
        var suggestions = new List<string>();

        if (TotalCustomersHelped >= 5)
            suggestions.Add("Great progress! Consider reviewing common missing fields to optimize your template.");

        if (AverageSentiment < 0)
            suggestions.Add("Sentiment trending negative. Consider adjusting conversation approach.");

        if (TotalCo2SavedGrams > 10)
            suggestions.Add($"You've saved {TotalCo2SavedGrams:F1}g CO₂ by using local AI. Keep it up!");

        PlannerSuggestions = suggestions.Count > 0
            ? string.Join("\n• ", ["Suggestions:", .. suggestions])
            : "Keep going! More insights will appear as you complete sessions.";
    }
}

public class SessionSummary
{
    public string SessionId { get; set; } = string.Empty;
    public string CustomerAlias { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public double TimeSavedMinutes { get; set; }
    public string TimeSavedDisplay => $"{TimeSavedMinutes:F2}";
    public double SentimentScore { get; set; }
    public string SentimentDisplay => SentimentScore >= 0
        ? $"+{SentimentScore:F2}"
        : $"{SentimentScore:F2}";
}
