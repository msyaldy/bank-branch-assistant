namespace BranchScribe.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using BranchScribe.ViewModels;
using BranchScribe.Models;

public sealed partial class SessionPage : Page
{
    public SessionViewModel ViewModel { get; }
    private DispatcherTimer? _transcriptTimer;
    private int _segmentIndex;

    private static readonly (string speaker, int seconds, string text)[] SampleSegments =
    [
        ("Advisor", 0, "Good morning, welcome to Contoso Banking. How can I help you today?"),
        ("Customer", 5, "Hi, I'm Sarah Mitchell. I'd like to discuss getting a mortgage for my first home."),
        ("Advisor", 12, "Lovely, congratulations! Let me pull up our fact-find form. Could you confirm your full name and date of birth?"),
        ("Customer", 20, "Sarah Jane Mitchell, born 15th March 1992."),
        ("Advisor", 30, "Thank you. And what is your current employment status?"),
        ("Customer", 35, "I'm employed full-time as a software engineer at Contoso Ltd."),
        ("Advisor", 45, "Great. And your annual income before tax?"),
        ("Customer", 50, "Sixty-two thousand pounds."),
        ("Advisor", 58, "Noted. Do you have any existing credit commitments — loans, credit cards?"),
        ("Customer", 65, "I have a car loan with about four thousand remaining, and a credit card with a two thousand limit."),
        ("Advisor", 78, "Understood. Now, regarding the property — do you have one in mind?"),
        ("Customer", 85, "Yes, it's a two-bed flat in Reading, listed at two hundred and eighty-five thousand."),
        ("Advisor", 95, "And how much deposit have you saved?"),
        ("Customer", 100, "Twenty-eight thousand five hundred — so about ten percent."),
        ("Advisor", 110, "Excellent. Do you have a preference on rate type — fixed or variable?"),
        ("Customer", 118, "I'd prefer a five-year fixed rate for stability."),
    ];

    public SessionPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<SessionViewModel>();
        this.InitializeComponent();

        // Defer simulation start until after layout completes to avoid XAML crashes
        this.Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        this.Loaded -= OnPageLoaded;
        if (ViewModel.TranscriptSegments.Count == 0)
            StartLiveSimulation();
    }

    private void StartLiveSimulation()
    {
        _segmentIndex = 0;
        ViewModel.IsRecording = true;

        // Simulate live transcript arriving every 3 seconds
        _transcriptTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3),
        };
        _transcriptTimer.Tick += OnTranscriptTick;
        _transcriptTimer.Start();

        // Deliver the first segment immediately then trigger NPU
        DeliverNextSegment();
    }

    private void OnTranscriptTick(object? sender, object e)
    {
        DeliverNextSegment();
    }

    private void DeliverNextSegment()
    {
        if (_segmentIndex >= SampleSegments.Length)
        {
            // All segments delivered — stop recording
            _transcriptTimer?.Stop();
            ViewModel.IsRecording = false;
            // Final NPU extraction
            _ = ViewModel.RunExtractionCommand.ExecuteAsync(null);
            return;
        }

        var (speaker, seconds, text) = SampleSegments[_segmentIndex];
        var segment = new TranscriptSegment(TimeSpan.FromSeconds(seconds), speaker, text);

        ViewModel.TranscriptSegments.Add(segment);
        ViewModel.CurrentSession.TranscriptSegments.Add(segment);
        _segmentIndex++;

        // Run instant keyword extraction on every segment (free, no NPU)
        ViewModel.RunQuickExtraction();

        // Trigger NPU extraction on first segment and every 3 segments after
        if (_segmentIndex == 1 || _segmentIndex % 3 == 0)
        {
            _ = ViewModel.RunExtractionCommand.ExecuteAsync(null);
        }
    }
}
