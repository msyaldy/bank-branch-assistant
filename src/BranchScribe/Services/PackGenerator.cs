namespace BranchScribe.Services;

using BranchScribe.Models;
using BranchScribe.Services.Interfaces;

public class PackGenerator : IPackGenerator
{
    private readonly ILocalLLM _llm;

    public PackGenerator(ILocalLLM llm)
    {
        _llm = llm;
    }

    public async Task<Pack> GenerateAsync(
        Session session,
        FormSchema schema,
        ExtractionResult extraction,
        CancellationToken ct = default)
    {
        // Generate pack immediately using template (instant)
        var pack = BuildTemplatePack(session, schema, extraction);

        // Try NPU enhancement with short timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        try
        {
            var request = new LlmRequest
            {
                SystemPrompt = "Generate a concise professional advisor summary of this meeting in 2-3 sentences.",
                UserPrompt = $"Customer: {session.CustomerAlias}\n" +
                             $"Fields captured: {string.Join(", ", extraction.Fields.Select(kv => $"{kv.Key}={kv.Value.Value}"))}\n" +
                             $"Missing: {string.Join(", ", extraction.MissingFieldIds)}",
                Temperature = 0.3,
                MaxTokens = 256,
            };

            var response = await _llm.CompleteAsync(request, cts.Token);
            if (!string.IsNullOrWhiteSpace(response.Content))
                pack.AdvisorSummary = response.Content;
        }
        catch (OperationCanceledException)
        {
            // NPU took too long — keep template summary
        }

        return pack;
    }

    private static Pack BuildTemplatePack(Session session, FormSchema schema, ExtractionResult extraction)
    {
        var capturedFields = extraction.Fields.Select(kv => $"• {kv.Key}: {kv.Value.Value}");
        var missingFields = extraction.MissingFieldIds.Select(id => $"• {id}");

        // Get customer name from extracted fields
        var customerName = extraction.Fields.TryGetValue("customer_name", out var nameField)
            ? nameField.Value
            : session.CustomerAlias;

        var capturedFieldsSummary = string.Join("\n", extraction.Fields.Select(kv =>
            $"  • {kv.Key.Replace("_", " ")}: {kv.Value.Value}"));

        var advisorSummary = $"Meeting with {customerName} on {session.StartedAt:d MMMM yyyy}.\n\n" +
                             $"Fields captured ({extraction.Fields.Count}/{schema.Fields.Count}):\n" +
                             string.Join("\n", capturedFields);

        if (extraction.MissingFieldIds.Count > 0)
            advisorSummary += $"\n\nOutstanding items ({extraction.MissingFieldIds.Count}):\n" +
                              string.Join("\n", missingFields);

        var pack = new Pack
        {
            SessionId = session.Id,
            CustomerName = customerName,
            CapturedFieldsSummary = capturedFieldsSummary,
            AdvisorSummary = advisorSummary,
            CustomerSummary = $"Dear {customerName},\n\n" +
                              $"Thank you for meeting with us today at Contoso Banking. " +
                              $"We have captured your key details and will be in touch with next steps.\n\n" +
                              $"Information confirmed: {extraction.Fields.Count} items\n" +
                              (extraction.MissingFieldIds.Count > 0
                                  ? $"We'll follow up on {extraction.MissingFieldIds.Count} remaining item(s).\n\n"
                                  : "\n") +
                              "Kind regards,\nYour Contoso Banking Advisor",
            MissingConfirmations = extraction.MissingFieldIds,
        };

        foreach (var (fieldId, fieldValue) in extraction.Fields)
        {
            if (fieldValue.Citations.Count > 0)
                pack.FieldCitations[fieldId] = fieldValue.Citations;
        }

        pack.ActionChecklist =
        [
            "Review extracted fields for accuracy",
            ..extraction.MissingFieldIds.Select(id => $"Follow up: capture {id.Replace("_", " ")}"),
            "Run affordability assessment",
            "Send customer summary via secure channel",
            "Schedule follow-up appointment if needed",
        ];

        return pack;
    }
}
