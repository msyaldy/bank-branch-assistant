namespace BranchScribe.Services.Interfaces;

using BranchScribe.Models;

public interface IFormExtractor
{
    Task<ExtractionResult> ExtractAsync(FormSchema schema, IReadOnlyList<TranscriptSegment> transcript, CancellationToken ct = default);
}
