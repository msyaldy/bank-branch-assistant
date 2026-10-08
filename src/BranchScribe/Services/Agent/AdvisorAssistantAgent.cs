namespace BranchScribe.Services.Agent;

using BranchScribe.Models;
using BranchScribe.Services.Interfaces;
using System.Text.Json;

public class AdvisorAssistantAgent : IAssistantAgent
{
    private readonly ILocalLLM _llm;
    private readonly IFormExtractor _formExtractor;
    private readonly INextQuestionPlanner _planner;

    private static readonly Dictionary<string, string> ToolDescriptions = new()
    {
        ["GetMissingFields"] = "Returns a list of form fields that have not yet been captured.",
        ["GetLowConfidenceFields"] = "Returns fields where extraction confidence is below threshold.",
        ["GetCitation"] = "Returns the transcript snippet and timestamp for a specific extracted field.",
        ["SummariseSession"] = "Generates a summary of the current meeting session.",
        ["DraftCustomerRecap"] = "Creates a customer-friendly recap of the meeting.",
    };

    public AdvisorAssistantAgent(ILocalLLM llm, IFormExtractor formExtractor, INextQuestionPlanner planner)
    {
        _llm = llm;
        _formExtractor = formExtractor;
        _planner = planner;
    }

    public async Task<AgentMessage> SendAsync(
        AgentThread thread,
        string userMessage,
        Session session,
        CancellationToken ct = default)
    {
        thread.Messages.Add(new AgentMessage { Role = AgentRole.User, Content = userMessage });

        // Detect tool calls from the user's message — these return instantly
        var toolResult = TryExecuteTool(userMessage, session);
        if (toolResult is not null)
        {
            thread.Messages.Add(new AgentMessage
            {
                Role = AgentRole.Tool,
                Content = toolResult,
                ToolName = DetectToolName(userMessage),
            });

            // Return tool result directly as assistant response (instant)
            var toolMessage = new AgentMessage
            {
                Role = AgentRole.Assistant,
                Content = toolResult,
            };
            thread.Messages.Add(toolMessage);
            return toolMessage;
        }

        // For general questions, try NPU with a short timeout, fallback to template
        var conversationContext = BuildConversationContext(thread, session);
        var request = new LlmRequest
        {
            SystemPrompt = BuildSystemPrompt(),
            UserPrompt = conversationContext,
            Temperature = 0.4,
            MaxTokens = 512,
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        string responseContent;
        try
        {
            var response = await _llm.CompleteAsync(request, cts.Token);
            responseContent = response.Content;
        }
        catch (OperationCanceledException)
        {
            // NPU took too long — provide template response
            responseContent = GenerateTemplateResponse(userMessage, session);
        }

        var assistantMessage = new AgentMessage
        {
            Role = AgentRole.Assistant,
            Content = responseContent,
        };

        thread.Messages.Add(assistantMessage);
        return assistantMessage;
    }

    private static string GenerateTemplateResponse(string userMessage, Session session)
    {
        var lower = userMessage.ToLowerInvariant();
        if (lower.Contains("help") || lower.Contains("what can"))
            return "I can help you with: missing fields, low confidence extractions, citations from the transcript, session summaries, and customer recaps. Just ask!";
        if (lower.Contains("how"))
            return $"Based on your session with {session.CustomerAlias}, I have {session.ExtractedFields.Count} fields captured from {session.TranscriptSegments.Count} transcript segments. Ask me about specific fields or missing information.";
        return $"I'm your on-device advisor assistant (running on NPU). I have context on your session with {session.CustomerAlias} — {session.ExtractedFields.Count} fields extracted so far. Try asking about missing fields, low confidence, or request a summary.";
    }

    private static string BuildSystemPrompt() =>
        """
        You are an Advisor Assistant for a bank branch meeting, powered by Phi Silica running entirely on-device.
        All processing stays local — no data leaves this machine. You help the advisor by:
        - Identifying missing form fields
        - Highlighting low-confidence extractions
        - Providing citations from the transcript
        - Summarising the meeting
        - Drafting customer recaps
        
        Available tools: GetMissingFields, GetLowConfidenceFields, GetCitation, SummariseSession, DraftCustomerRecap.
        When the user asks about missing fields, low confidence, citations, summaries, or recaps, use the relevant tool.
        Be concise and professional.
        """;

    private static string BuildConversationContext(AgentThread thread, Session session)
    {
        var recentMessages = thread.Messages.TakeLast(10);
        var context = $"Session: {session.CustomerAlias}, started {session.StartedAt:g}\n" +
                      $"Transcript segments: {session.TranscriptSegments.Count}\n" +
                      $"Extracted fields: {session.ExtractedFields.Count}\n\n";

        foreach (var msg in recentMessages)
        {
            context += $"[{msg.Role}]: {msg.Content}\n";
        }

        return context;
    }

    private static string? TryExecuteTool(string userMessage, Session session)
    {
        var lower = userMessage.ToLowerInvariant();

        if (lower.Contains("missing"))
        {
            var missing = session.ExtractedFields.Count == 0
                ? "No extraction performed yet."
                : "Missing fields: " + string.Join(", ",
                    session.ExtractedFields.Where(kv => string.IsNullOrEmpty(kv.Value.Value))
                        .Select(kv => kv.Key));
            return missing;
        }

        if (lower.Contains("low confidence"))
        {
            var lowConf = session.ExtractedFields
                .Where(kv => kv.Value.Confidence < 0.7)
                .Select(kv => $"{kv.Key}: {kv.Value.Confidence:P0}")
                .ToList();
            return lowConf.Count > 0
                ? "Low confidence fields: " + string.Join(", ", lowConf)
                : "All extracted fields have acceptable confidence.";
        }

        if (lower.Contains("citation") || lower.Contains("evidence"))
        {
            var citations = session.ExtractedFields
                .Where(kv => kv.Value.Citations.Count > 0)
                .SelectMany(kv => kv.Value.Citations.Select(c =>
                    $"{kv.Key} @ {c.Timestamp:mm\\:ss}: \"{c.Snippet}\""))
                .ToList();
            return citations.Count > 0
                ? string.Join("\n", citations)
                : "No citations available yet.";
        }

        if (lower.Contains("summar"))
            return $"Session with {session.CustomerAlias}, {session.TranscriptSegments.Count} segments recorded, {session.ExtractedFields.Count} fields extracted.";

        if (lower.Contains("recap") || lower.Contains("customer"))
            return $"Dear {session.CustomerAlias}, thank you for meeting with us today. We discussed your requirements and captured key information. Your advisor will follow up with next steps.";

        return null;
    }

    private static string? DetectToolName(string userMessage)
    {
        var lower = userMessage.ToLowerInvariant();
        if (lower.Contains("missing")) return "GetMissingFields";
        if (lower.Contains("low confidence")) return "GetLowConfidenceFields";
        if (lower.Contains("citation") || lower.Contains("evidence")) return "GetCitation";
        if (lower.Contains("summar")) return "SummariseSession";
        if (lower.Contains("recap") || lower.Contains("customer")) return "DraftCustomerRecap";
        return null;
    }
}
