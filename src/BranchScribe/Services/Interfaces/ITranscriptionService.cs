namespace BranchScribe.Services.Interfaces;

using BranchScribe.Models;

public interface ITranscriptionService
{
    event EventHandler<TranscriptSegment>? SegmentReceived;
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
    bool IsRunning { get; }
}
