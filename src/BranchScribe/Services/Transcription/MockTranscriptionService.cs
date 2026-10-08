namespace BranchScribe.Services.Transcription;

using BranchScribe.Models;
using BranchScribe.Services.Interfaces;

public class MockTranscriptionService : ITranscriptionService
{
    private CancellationTokenSource? _cts;
    private Task? _runningTask;

    public event EventHandler<TranscriptSegment>? SegmentReceived;
    public bool IsRunning => _runningTask is not null && !_runningTask.IsCompleted;

    private static readonly (string Speaker, string Text)[] SampleTranscript =
    [
        ("Advisor", "Good morning! Welcome to the branch. How can I help you today?"),
        ("Customer", "Hi, I'd like to discuss mortgage options. My partner and I are looking to buy our first home."),
        ("Advisor", "Congratulations! Let me pull up our fact-find form. Can I start with your full name?"),
        ("Customer", "Sure, it's Jane Smith."),
        ("Advisor", "And your date of birth?"),
        ("Customer", "March 15th, 1990."),
        ("Advisor", "Thank you. And what's your current annual income before tax?"),
        ("Customer", "I earn £52,000 and my partner earns £38,000."),
        ("Advisor", "Great. Do either of you have any outstanding debts or loans?"),
        ("Customer", "I have a car loan with about £4,000 remaining, and a student loan."),
        ("Advisor", "Understood. What property value range are you considering?"),
        ("Customer", "We're looking at properties between £280,000 and £320,000."),
        ("Advisor", "And how much deposit have you saved?"),
        ("Customer", "We have £35,000 saved up."),
        ("Advisor", "That's a solid deposit. Do you have a preference for fixed or variable rate?"),
        ("Customer", "We'd prefer a fixed rate for stability, probably a 5-year fix."),
    ];

    public Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _runningTask = ProduceSegmentsAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = null;
        }
        if (_runningTask is not null)
        {
            try { await _runningTask; } catch (OperationCanceledException) { }
            _runningTask = null;
        }
    }

    private async Task ProduceSegmentsAsync(CancellationToken ct)
    {
        var elapsed = TimeSpan.Zero;
        foreach (var (speaker, text) in SampleTranscript)
        {
            if (ct.IsCancellationRequested) break;
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            elapsed += TimeSpan.FromSeconds(2);
            var segment = new TranscriptSegment(elapsed, speaker, text);
            SegmentReceived?.Invoke(this, segment);
        }
    }
}
