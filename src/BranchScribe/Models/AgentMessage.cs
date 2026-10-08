namespace BranchScribe.Models;

public enum AgentRole { User, Assistant, System, Tool }

public class AgentMessage
{
    public AgentRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ToolName { get; set; }
    public string? ToolCallId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
}

public class AgentThread
{
    public string SessionId { get; set; } = string.Empty;
    public List<AgentMessage> Messages { get; set; } = [];
}
