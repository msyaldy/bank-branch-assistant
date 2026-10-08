namespace BranchScribe.Models;

public class LlmRequest
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 2048;
}

public class LlmResponse
{
    public string Content { get; set; } = string.Empty;
    public ModerationResult? Moderation { get; set; }
    public double InferenceTimeMs { get; set; }
    public bool IsFromCache { get; set; }
}

public class ModerationResult
{
    public bool IsFlagged { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
