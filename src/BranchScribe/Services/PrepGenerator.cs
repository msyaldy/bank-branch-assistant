namespace BranchScribe.Services;

using BranchScribe.Models;
using BranchScribe.Services.Interfaces;
using BranchScribe.ViewModels;
using System.Text.Json;

/// <summary>
/// Uses on-device Phi Silica (via ILocalLLM) to generate personalised prep items
/// for the advisor's upcoming appointments.
/// </summary>
public class PrepGenerator : IPrepGenerator
{
    private readonly ILocalLLM _llm;

    public PrepGenerator(ILocalLLM llm)
    {
        _llm = llm;
    }

    public async Task<IReadOnlyList<PrepItem>> GeneratePrepItemsAsync(
        IReadOnlyList<AppointmentItem> appointments,
        CancellationToken ct = default)
    {
        // Filter to appointments that need prep (not gaps, not completed)
        var needsPrep = appointments
            .Where(a => !a.IsGap && a.Status != "Completed" && !string.IsNullOrEmpty(a.Type))
            .ToList();

        if (needsPrep.Count == 0)
            return [];

        var appointmentList = string.Join("\n", needsPrep.Select(a =>
            $"- {a.Time}: {a.CustomerName} — {a.Type} (Status: {a.Status})"));

        var request = new LlmRequest
        {
            SystemPrompt = """
                You are a banking advisor preparation assistant running on-device via Phi Silica.
                Generate preparation tasks for each upcoming appointment.
                For each appointment, provide:
                - What documents or information to gather
                - Key questions to review beforehand
                - Any compliance checks needed
                
                Respond ONLY with a JSON array where each item has:
                {"title": "Customer — Type", "description": "preparation steps", "priority": "High|Medium|Low"}
                
                Priority rules:
                - High: Status is "Prep needed" or complex products (Mortgage, Business Loan)
                - Medium: Standard products (ISA, Remortgage, Savings)
                - Low: Simple or admin tasks
                
                Keep descriptions concise (2-3 bullet points). No markdown, just plain text with bullet points using •.
                """,
            UserPrompt = $"Generate prep items for today's appointments:\n{appointmentList}",
            Temperature = 0.3,
            MaxTokens = 1024,
        };

        var response = await _llm.CompleteAsync(request, ct);
        return ParsePrepItems(response.Content, needsPrep);
    }

    private static IReadOnlyList<PrepItem> ParsePrepItems(
        string llmOutput, List<AppointmentItem> appointments)
    {
        try
        {
            // Try parsing as JSON array
            var items = JsonSerializer.Deserialize<List<PrepItemDto>>(llmOutput,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (items is not null && items.Count > 0)
            {
                return items.Select(dto => new PrepItem
                {
                    Icon = GetIconForPriority(dto.Priority),
                    Title = dto.Title ?? "Appointment prep",
                    Description = dto.Description ?? "Review customer records before appointment.",
                    Priority = dto.Priority ?? "Medium",
                    PriorityColor = GetColorForPriority(dto.Priority),
                }).ToList();
            }
        }
        catch (JsonException)
        {
            // LLM didn't return valid JSON — fall back to structured generation
        }

        // Fallback: generate structured items from appointment data
        return GenerateFallbackPrepItems(appointments);
    }

    private static IReadOnlyList<PrepItem> GenerateFallbackPrepItems(
        List<AppointmentItem> appointments)
    {
        var items = new List<PrepItem>();

        foreach (var apt in appointments)
        {
            var (desc, priority) = apt.Type?.ToLowerInvariant() switch
            {
                "mortgage" => (
                    "• Review affordability criteria and current rates\n• Check customer's existing relationship\n• Prepare fact-find form",
                    "High"),
                "business loan" => (
                    "• Pull trading history and cash flow data\n• Review business account activity\n• Check lending criteria eligibility",
                    "High"),
                "isa transfer" => (
                    "• Confirm existing ISA provider details\n• Check transfer-in form availability\n• Verify current year allowance eligibility",
                    "Medium"),
                "remortgage" => (
                    "• Check current fixed-rate end date\n• Review early repayment charges\n• Prepare product comparison sheet",
                    "Medium"),
                "joint account" => (
                    "• Confirm both parties' ID requirements\n• Review joint account terms\n• Check switching service eligibility",
                    "Medium"),
                "savings review" => (
                    "• Pull current savings balances\n• Check for better rate products\n• Review ISA allowance usage",
                    "Low"),
                _ => (
                    "• Review customer CRM notes\n• Check for outstanding actions\n• Prepare relevant forms",
                    "Medium"),
            };

            items.Add(new PrepItem
            {
                Icon = GetIconForPriority(priority),
                Title = $"{apt.CustomerName} – {apt.Type}",
                Description = desc,
                Priority = priority,
                PriorityColor = GetColorForPriority(priority),
            });
        }

        return items;
    }

    private static string GetIconForPriority(string? priority) => priority switch
    {
        "High" => "\uE8A5",   // Document
        "Medium" => "\uE77B", // Contact
        "Low" => "\uE715",    // Settings
        _ => "\uE8A5",
    };

    private static string GetColorForPriority(string? priority) => priority switch
    {
        "High" => "#EF4444",
        "Medium" => "#F59E0B",
        "Low" => "#6B7280",
        _ => "#F59E0B",
    };

    private class PrepItemDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Priority { get; set; }
    }
}
