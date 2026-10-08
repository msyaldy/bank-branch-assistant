namespace BranchScribe.Models;

public record TranscriptSegment(
    TimeSpan Timestamp,
    string Speaker,
    string Text);
