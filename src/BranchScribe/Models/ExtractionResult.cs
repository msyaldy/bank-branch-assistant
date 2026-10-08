namespace BranchScribe.Models;

public class ExtractionResult
{
    public Dictionary<string, ExtractedFieldValue> Fields { get; set; } = [];
    public List<string> MissingFieldIds { get; set; } = [];
}

public class ExtractedFieldValue
{
    public string FieldId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<Citation> Citations { get; set; } = [];
}

public class Citation
{
    public int SegmentIndex { get; set; }
    public TimeSpan Timestamp { get; set; }
    public string Snippet { get; set; } = string.Empty;
}
