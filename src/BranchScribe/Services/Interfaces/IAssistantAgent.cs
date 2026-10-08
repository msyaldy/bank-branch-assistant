namespace BranchScribe.Services.Interfaces;

using BranchScribe.Models;

public interface IAssistantAgent
{
    Task<AgentMessage> SendAsync(AgentThread thread, string userMessage, Session session, CancellationToken ct = default);
}
