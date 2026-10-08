namespace BranchScribe.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BranchScribe.Models;
using BranchScribe.Services.Interfaces;
using BranchScribe.Services.Extraction;
using BranchScribe.Services.Planning;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

public partial class SessionViewModel : ObservableObject
{
    private readonly ITranscriptionService _transcription;
    private readonly IFormExtractor _formExtractor;
    private readonly INextQuestionPlanner _planner;
    private readonly IAssistantAgent _assistant;
    private readonly IMetricsEvaluator _metricsEvaluator;
    private readonly IPackGenerator _packGenerator;
    private readonly FormSchema _schema;
    private readonly AgentThread _agentThread = new();
    private int _inferenceCount;
    private bool _extractionInProgress;

    [ObservableProperty]
    private Session _currentSession = new();

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _nextBestQuestion = string.Empty;

    [ObservableProperty]
    private string _assistantInput = string.Empty;

    [ObservableProperty]
    private string _lastAssistantResponse = string.Empty;

    [ObservableProperty]
    private bool _isAssistantPanelOpen;

    [ObservableProperty]
    private bool _isNpuProcessing;

    [ObservableProperty]
    private double _formCompletionPercent;

    public string FormCompletionDisplay => $"{FormCompletionPercent:F0}% complete";

    partial void OnFormCompletionPercentChanged(double value) =>
        OnPropertyChanged(nameof(FormCompletionDisplay));

    [ObservableProperty]
    private Pack? _generatedPack;

    public ObservableCollection<TranscriptSegment> TranscriptSegments { get; } = [];
    public ObservableCollection<string> MissingFields { get; } = [];
    public ObservableCollection<ExtractedFieldValue> ExtractedFields { get; } = [];
    public ObservableCollection<AgentMessage> AssistantMessages { get; } = [];

    public SessionViewModel(
        ITranscriptionService transcription,
        IFormExtractor formExtractor,
        INextQuestionPlanner planner,
        IAssistantAgent assistant,
        IMetricsEvaluator metricsEvaluator,
        IPackGenerator packGenerator,
        FormSchema schema)
    {
        _transcription = transcription;
        _formExtractor = formExtractor;
        _planner = planner;
        _assistant = assistant;
        _metricsEvaluator = metricsEvaluator;
        _packGenerator = packGenerator;
        _schema = schema;

        _transcription.SegmentReceived += OnSegmentReceived;
        _agentThread.SessionId = CurrentSession.Id;
    }

    private async void OnSegmentReceived(object? sender, TranscriptSegment segment)
    {
        App.Current.DispatcherQueue?.TryEnqueue(() =>
        {
            TranscriptSegments.Add(segment);
            CurrentSession.TranscriptSegments.Add(segment);
        });

        // Re-extract after every few segments
        if (TranscriptSegments.Count % 3 == 0)
        {
            await RunExtractionAsync();
        }
    }

    [RelayCommand]
    private async Task StartSessionAsync()
    {
        CurrentSession = new Session
        {
            AdvisorId = "advisor-001",
            CustomerAlias = "Customer",
        };
        _agentThread.SessionId = CurrentSession.Id;
        _agentThread.Messages.Clear();

        TranscriptSegments.Clear();
        MissingFields.Clear();
        ExtractedFields.Clear();
        AssistantMessages.Clear();
        _inferenceCount = 0;

        IsRecording = true;
        await _transcription.StartAsync();
    }

    [RelayCommand]
    private async Task StopSessionAsync()
    {
        await _transcription.StopAsync();
        IsRecording = false;
        CurrentSession.EndedAt = DateTimeOffset.Now;

        // Final extraction
        await RunExtractionAsync();

        // Calculate metrics
        CurrentSession.Metrics = _metricsEvaluator.CalculateSessionMetrics(CurrentSession, _inferenceCount);
    }

    [RelayCommand]
    private async Task GeneratePackAsync()
    {
        var extraction = new ExtractionResult
        {
            Fields = new Dictionary<string, ExtractedFieldValue>(CurrentSession.ExtractedFields),
            MissingFieldIds = MissingFields.ToList(),
        };
        GeneratedPack = await _packGenerator.GenerateAsync(CurrentSession, _schema, extraction);

        // Load into ReviewPackViewModel and navigate
        var reviewVm = App.Current.Services.GetRequiredService<ReviewPackViewModel>();
        reviewVm.LoadPack(GeneratedPack);
    }

    [RelayCommand]
    private async Task SendAssistantMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(AssistantInput)) return;

        var userMsg = AssistantInput;
        AssistantInput = string.Empty;

        var response = await _assistant.SendAsync(_agentThread, userMsg, CurrentSession);
        _inferenceCount++;

        App.Current.DispatcherQueue?.TryEnqueue(() =>
        {
            AssistantMessages.Add(new AgentMessage { Role = AgentRole.User, Content = userMsg });
            AssistantMessages.Add(response);
            LastAssistantResponse = response.Content;
        });
    }

    [RelayCommand]
    private void ToggleAssistantPanel()
    {
        IsAssistantPanelOpen = !IsAssistantPanelOpen;
    }

    /// <summary>
    /// Instant keyword-based extraction — called on every new segment.
    /// No NPU, no async — updates UI immediately.
    /// </summary>
    public void RunQuickExtraction()
    {
        var quickExtraction = FormExtractor.QuickExtract(
            _schema, CurrentSession.TranscriptSegments);
        UpdateExtractionUI(quickExtraction);
    }

    [RelayCommand]
    private async Task RunExtractionAsync()
    {
        // Skip if extraction already in progress — prevents NPU overload
        if (_extractionInProgress)
            return;

        _extractionInProgress = true;
        IsNpuProcessing = true;

        try
        {
            // Run NPU extraction to enhance keyword results
            var npuExtraction = await _formExtractor.ExtractAsync(
                _schema, CurrentSession.TranscriptSegments);
            _inferenceCount++;

            App.Current.DispatcherQueue?.TryEnqueue(() =>
            {
                UpdateExtractionUI(npuExtraction);
            });
        }
        finally
        {
            _extractionInProgress = false;
            IsNpuProcessing = false;
        }
    }

    private void UpdateExtractionUI(ExtractionResult extraction)
    {
        CurrentSession.ExtractedFields = extraction.Fields;
        ExtractedFields.Clear();
        foreach (var field in extraction.Fields.Values)
            ExtractedFields.Add(field);

        // Show missing fields as readable labels
        MissingFields.Clear();
        var missingFieldsList = NextQuestionPlanner.GetResolvableMissingFields(_schema, extraction);
        foreach (var field in missingFieldsList)
            MissingFields.Add(field.Label);

        // Calculate completion percentage
        int total = _schema.Fields.Count(f => f.Required);
        int filled = extraction.Fields.Count;
        FormCompletionPercent = total > 0 ? (filled * 100.0 / total) : 0;

        // Next best question uses prompt template to guide the advisor
        if (missingFieldsList.Count > 0)
        {
            var next = missingFieldsList.First();
            var prompt = next.PromptTemplate ?? $"Could you tell me about your {next.Label.ToLower()}?";
            NextBestQuestion = $"💡 Ask: \"{prompt}\"  ({missingFieldsList.Count} field{(missingFieldsList.Count > 1 ? "s" : "")} remaining)";
        }
        else
        {
            NextBestQuestion = "✅ All required fields have been captured. You can generate the pack.";
        }
    }
}
