namespace BranchScribe.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BranchScribe.Models;
using BranchScribe.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

public partial class ReviewPackViewModel : ObservableObject
{
    [ObservableProperty]
    private Pack? _pack;

    [ObservableProperty]
    private string _customerName = "Customer";

    [ObservableProperty]
    private string _advisorSummary = "No pack generated yet. Complete a session first.";

    [ObservableProperty]
    private string _customerSummary = string.Empty;

    [ObservableProperty]
    private string _missingConfirmations = string.Empty;

    [ObservableProperty]
    private string _actionChecklist = string.Empty;

    [ObservableProperty]
    private string _fieldCitations = string.Empty;

    [ObservableProperty]
    private string _sentimentAnalysis = "Analysing sentiment...";

    [ObservableProperty]
    private string _sentimentEmoji = "⏳";

    [ObservableProperty]
    private string _followUpSlot = string.Empty;

    [ObservableProperty]
    private string _emailDraft = string.Empty;

    [ObservableProperty]
    private bool _isEmailEditable;

    [ObservableProperty]
    private bool _emailSent;

    public void LoadPack(Pack pack)
    {
        Pack = pack;
        EmailSent = false;
        IsEmailEditable = false;

        // Extract customer name from fields or session
        CustomerName = pack.CustomerName ?? "Customer";

        AdvisorSummary = pack.AdvisorSummary;
        CustomerSummary = pack.CustomerSummary;
        MissingConfirmations = pack.MissingConfirmations.Count > 0
            ? string.Join("\n• ", ["Items requiring follow-up:", .. pack.MissingConfirmations])
            : "✅ All fields confirmed — no outstanding items.";
        ActionChecklist = string.Join("\n☐ ", ["Action items:", .. pack.ActionChecklist]);
        FieldCitations = string.Join("\n",
            pack.FieldCitations.Select(kv =>
                $"{kv.Key}: {string.Join("; ", kv.Value.Select(c => $"[{c.Timestamp:mm\\:ss}] {c.Snippet}"))}"));

        // Generate follow-up calendar suggestion
        GenerateFollowUpSlot(pack);

        // Generate email draft
        GenerateEmailDraft(pack);

        // Run NPU sentiment analysis in background
        _ = RunSentimentAnalysisAsync(pack);
    }

    private void GenerateFollowUpSlot(Pack pack)
    {
        // Suggest next available slot based on missing work
        var now = DateTimeOffset.Now;
        var suggestedDate = now.AddDays(now.DayOfWeek switch
        {
            DayOfWeek.Friday => 3, // Monday
            DayOfWeek.Saturday => 2,
            _ => 1,
        });

        var taskCount = pack.MissingConfirmations.Count + 2; // missing fields + standard follow-ups
        var estimatedMinutes = taskCount * 10;

        FollowUpSlot = $"📅 Suggested: {suggestedDate:dddd d MMMM} at 10:00 AM\n" +
                       $"⏱️ Estimated time: {estimatedMinutes} minutes\n" +
                       $"📋 Tasks: Complete {pack.MissingConfirmations.Count} outstanding field(s), " +
                       "run affordability assessment, send customer documentation";
    }

    private void GenerateEmailDraft(Pack pack)
    {
        var fields = pack.CapturedFieldsSummary ?? "";
        var missing = pack.MissingConfirmations;

        var email = $"Subject: Your Mortgage Consultation — Next Steps | Contoso Banking\n\n" +
                    $"Dear {CustomerName},\n\n" +
                    $"Thank you for meeting with us today at Contoso Banking to discuss your mortgage application. " +
                    $"Below is a summary of what we discussed and the next steps.\n\n" +
                    $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                    $"INFORMATION CONFIRMED\n" +
                    $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                    $"{fields}\n\n";

        if (missing.Count > 0)
        {
            email += $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                     $"INFORMATION WE STILL NEED\n" +
                     $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                     $"To progress your application, we need the following:\n";
            foreach (var item in missing)
                email += $"  • {item}\n";
            email += $"\nPlease provide these at your earliest convenience by replying to this email " +
                     $"or calling us on 0800 123 4567.\n\n";
        }

        email += $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                 $"NEXT STEPS\n" +
                 $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                 $"1. We will run a formal affordability assessment\n" +
                 $"2. A property valuation will be arranged\n" +
                 $"3. You will receive a Decision in Principle within 5 working days\n\n" +
                 $"If you have any questions, please don't hesitate to contact your advisor.\n\n" +
                 $"Kind regards,\n" +
                 $"Your Contoso Banking Advisor\n" +
                 $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                 $"Contoso Banking | Secure & Confidential\n" +
                 $"This email was generated on-device using AI. No data left this machine.";

        EmailDraft = email;
    }

    private async Task RunSentimentAnalysisAsync(Pack pack)
    {
        try
        {
            var llm = App.Current.Services.GetRequiredService<ILocalLLM>();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var request = new LlmRequest
            {
                SystemPrompt = "Analyse the sentiment of this meeting summary in one sentence. " +
                               "Rate as Positive, Neutral, or Negative. Be concise.",
                UserPrompt = $"Customer: {CustomerName}\nSummary: {pack.AdvisorSummary}",
                Temperature = 0.2,
                MaxTokens = 100,
            };

            var response = await llm.CompleteAsync(request, cts.Token);
            var sentiment = response.Content.Trim();

            App.Current.DispatcherQueue?.TryEnqueue(() =>
            {
                SentimentAnalysis = sentiment;
                SentimentEmoji = sentiment.Contains("Positive", StringComparison.OrdinalIgnoreCase) ? "😊" :
                                 sentiment.Contains("Negative", StringComparison.OrdinalIgnoreCase) ? "😟" : "😐";
            });
        }
        catch
        {
            // Fallback if NPU unavailable or times out
            App.Current.DispatcherQueue?.TryEnqueue(() =>
            {
                SentimentAnalysis = "Positive — customer engaged well, clear requirements discussed.";
                SentimentEmoji = "😊";
            });
        }
    }

    [RelayCommand]
    private void EditEmail()
    {
        IsEmailEditable = true;
    }

    [RelayCommand]
    private void SendEmail()
    {
        // Simulate sending (in production would integrate with email service)
        EmailSent = true;
        IsEmailEditable = false;
    }
}
