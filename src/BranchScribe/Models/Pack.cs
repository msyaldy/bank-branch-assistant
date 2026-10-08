namespace BranchScribe.Models;

public class Pack
{
    public string SessionId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string AdvisorSummary { get; set; } = string.Empty;
    public string CustomerSummary { get; set; } = string.Empty;
    public string CapturedFieldsSummary { get; set; } = string.Empty;
    public List<string> ActionChecklist { get; set; } = [];
    public Dictionary<string, List<Citation>> FieldCitations { get; set; } = [];
    public List<string> MissingConfirmations { get; set; } = [];
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.Now;
}
