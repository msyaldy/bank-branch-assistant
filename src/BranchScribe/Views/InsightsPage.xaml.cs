namespace BranchScribe.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using BranchScribe.ViewModels;
using BranchScribe.Models;

public sealed partial class InsightsPage : Page
{
    public InsightsViewModel ViewModel { get; }

    public InsightsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<InsightsViewModel>();
        if (ViewModel.SessionHistory.Count == 0)
            LoadSampleData();
        this.InitializeComponent();
    }

    private void LoadSampleData()
    {
        var sessions = new (string alias, double timeSaved, double co2Saved, double co2PerInf, double sentiment)[]
        {
            ("Sarah Mitchell", 12.5, 0.42, 0.084, 0.82),
            ("James Cooper", 8.0, 0.38, 0.076, 0.65),
            ("Emily Watson", 15.0, 0.51, 0.085, 0.91),
            ("David Chen", 10.0, 0.44, 0.088, 0.73),
            ("Rachel Adams", 11.5, 0.39, 0.078, 0.88),
        };

        foreach (var (alias, timeSaved, co2Saved, co2PerInf, sentiment) in sessions)
        {
            var session = new Session
            {
                AdvisorId = "advisor-001",
                CustomerAlias = alias,
                StartedAt = DateTimeOffset.Now.AddHours(-sessions.Length),
                EndedAt = DateTimeOffset.Now.AddHours(-sessions.Length + 1),
                Metrics = new Metrics
                {
                    TimeSavedMinutes = timeSaved,
                    Co2SavedEstimateGrams = co2Saved,
                    Co2PerInferenceGrams = co2PerInf,
                    CustomersHelped = 1,
                    SentimentScore = sentiment,
                },
            };
            ViewModel.AddSessionMetrics(session);
        }
    }
}
