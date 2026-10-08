namespace BranchScribe.Models;

public class Session
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? EndedAt { get; set; }
    public string AdvisorId { get; set; } = string.Empty;
    public string CustomerAlias { get; set; } = string.Empty;
    public List<TranscriptSegment> TranscriptSegments { get; set; } = [];
    public Dictionary<string, ExtractedFieldValue> ExtractedFields { get; set; } = [];
    public Metrics Metrics { get; set; } = new();
}
