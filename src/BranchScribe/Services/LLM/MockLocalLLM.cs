namespace BranchScribe.Services.LLM;

using BranchScribe.Models;
using BranchScribe.Services.Interfaces;
using System.Runtime.CompilerServices;

public class MockLocalLLM : ILocalLLM
{
    public string ProviderId => "MockLocal";

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task MakeAvailableAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        await Task.Delay(200, ct); // simulate inference time
        var content = GenerateMockResponse(request.UserPrompt);
        return new LlmResponse
        {
            Content = content,
            Moderation = new ModerationResult { IsFlagged = false },
            InferenceTimeMs = 200,
        };
    }

    public async IAsyncEnumerable<string> CompleteStreamingAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var words = GenerateMockResponse(request.UserPrompt).Split(' ');
        foreach (var word in words)
        {
            if (ct.IsCancellationRequested) yield break;
            await Task.Delay(50, ct);
            yield return word + " ";
        }
    }

    private static string GenerateMockResponse(string prompt)
    {
        if (prompt.Contains("prep", StringComparison.OrdinalIgnoreCase) &&
            prompt.Contains("appointment", StringComparison.OrdinalIgnoreCase))
            return """[{"title":"James Cooper – ISA Transfer","description":"• Review existing ISA provider details and transfer-in forms\n• Confirm current year allowance eligibility\n• Check for any exit fees with current provider","priority":"High"},{"title":"David Chen – Business Loan","description":"• Pull 2-year trading history and cash flow statements\n• Review business account activity for last 6 months\n• Check lending criteria and prepare affordability assessment","priority":"High"},{"title":"Emily Watson – Joint Account","description":"• Confirm both parties' ID verification status\n• Review joint account terms and switching options\n• Prepare direct debit transfer checklist","priority":"Medium"},{"title":"Rachel Adams – Remortgage","description":"• Check current fixed-rate end date and ERC window\n• Prepare product comparison with latest rates\n• Review property valuation requirements","priority":"Medium"},{"title":"Michael Brown – Savings Review","description":"• Pull current savings balances across all accounts\n• Check for better rate products available\n• Review ISA allowance usage for current tax year","priority":"Low"}]""";

        if (prompt.Contains("Extract", StringComparison.OrdinalIgnoreCase) &&
            prompt.Contains("Transcript:", StringComparison.OrdinalIgnoreCase))
            return ExtractFieldsFromTranscriptContext(prompt);

        if (prompt.Contains("missing", StringComparison.OrdinalIgnoreCase))
            return "The following fields are still missing: employment_status, employer_name, monthly_expenses, credit_score_consent.";

        if (prompt.Contains("summary", StringComparison.OrdinalIgnoreCase) || prompt.Contains("pack", StringComparison.OrdinalIgnoreCase))
            return "Meeting with Jane Smith regarding first-time buyer mortgage. Combined household income £90,000. Deposit of £35,000 available. Looking at properties £280-320k range with preference for 5-year fixed rate. Outstanding car loan (£4k) noted. Next steps: confirm employment details, run affordability calculation, and present product options.";

        if (prompt.Contains("sentiment", StringComparison.OrdinalIgnoreCase))
            return "0.75";

        return "I can help you with that. Based on the session transcript, here is what I found relevant to your query.";
    }

    /// <summary>
    /// Simulates Phi on-device extraction by scanning the transcript text in the prompt
    /// and only returning fields that have actual evidence in the conversation so far.
    /// </summary>
    private static string ExtractFieldsFromTranscriptContext(string prompt)
    {
        var fields = new Dictionary<string, string>();

        // Only extract fields whose evidence appears in the transcript portion of the prompt
        if (prompt.Contains("Sarah Jane Mitchell", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("I'm Sarah Mitchell", StringComparison.OrdinalIgnoreCase))
            fields["customer_name"] = "Sarah Jane Mitchell";

        if (prompt.Contains("15th March 1992", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("born 15th March", StringComparison.OrdinalIgnoreCase))
            fields["date_of_birth"] = "15/03/1992";

        if (prompt.Contains("employed full-time", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("software engineer", StringComparison.OrdinalIgnoreCase))
            fields["employment_status"] = "Employed full-time";

        if (prompt.Contains("Contoso Ltd", StringComparison.OrdinalIgnoreCase))
            fields["employer_name"] = "Contoso Ltd";

        if (prompt.Contains("sixty-two thousand", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("62,000", StringComparison.OrdinalIgnoreCase))
            fields["annual_income"] = "£62,000";

        if (prompt.Contains("car loan", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("credit card", StringComparison.OrdinalIgnoreCase))
            fields["outstanding_debts"] = "Car loan £4,000 remaining, credit card £2,000 limit";

        if (prompt.Contains("two hundred and eighty-five thousand", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("285,000", StringComparison.OrdinalIgnoreCase))
            fields["property_value_range"] = "£285,000";

        if (prompt.Contains("twenty-eight thousand five hundred", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("28,500", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("ten percent", StringComparison.OrdinalIgnoreCase))
            fields["deposit_amount"] = "£28,500";

        if (prompt.Contains("five-year fixed", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("fixed rate for stability", StringComparison.OrdinalIgnoreCase))
            fields["rate_preference"] = "5-year fixed";

        // Return only the fields found so far
        var json = System.Text.Json.JsonSerializer.Serialize(fields);
        return json;
    }
}
