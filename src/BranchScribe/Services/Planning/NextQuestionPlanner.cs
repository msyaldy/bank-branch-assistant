namespace BranchScribe.Services.Planning;

using BranchScribe.Models;
using BranchScribe.Services.Interfaces;

public class NextQuestionPlanner : INextQuestionPlanner
{
    private readonly ILocalLLM _llm;

    public NextQuestionPlanner(ILocalLLM llm)
    {
        _llm = llm;
    }

    public async Task<string> GetNextQuestionAsync(
        FormSchema schema,
        ExtractionResult currentExtraction,
        CancellationToken ct = default)
    {
        var missingFields = GetResolvableMissingFields(schema, currentExtraction);
        if (missingFields.Count == 0)
            return "All required fields have been captured. You can review the form.";

        var nextField = missingFields.First();
        if (!string.IsNullOrEmpty(nextField.PromptTemplate))
            return nextField.PromptTemplate;

        var request = new LlmRequest
        {
            SystemPrompt = "You are a helpful advisor assistant. Generate a natural, conversational question to ask the customer.",
            UserPrompt = $"Generate a question to capture: {nextField.Label} (type: {nextField.Type}). Be polite and conversational.",
            Temperature = 0.5,
        };

        var response = await _llm.CompleteAsync(request, ct);
        return response.Content;
    }

    public Task<List<string>> GetMissingQuestionsAsync(
        FormSchema schema,
        ExtractionResult currentExtraction,
        CancellationToken ct = default)
    {
        var missingFields = GetResolvableMissingFields(schema, currentExtraction);
        var questions = missingFields.Select(f =>
            !string.IsNullOrEmpty(f.PromptTemplate)
                ? f.PromptTemplate
                : $"Please ask about: {f.Label}").ToList();

        return Task.FromResult(questions);
    }

    /// <summary>
    /// Returns missing fields whose dependencies are all satisfied.
    /// </summary>
    public static List<FormField> GetResolvableMissingFields(FormSchema schema, ExtractionResult extraction)
    {
        var missingFieldIds = new HashSet<string>(
            schema.Fields
                .Where(f => f.Required && !extraction.Fields.ContainsKey(f.FieldId))
                .Select(f => f.FieldId));

        // Add explicitly missing fields from extraction
        foreach (var id in extraction.MissingFieldIds)
            missingFieldIds.Add(id);

        return schema.Fields
            .Where(f => missingFieldIds.Contains(f.FieldId))
            .Where(f => f.DependsOn.Count == 0 ||
                        f.DependsOn.All(dep => extraction.Fields.ContainsKey(dep)))
            .ToList();
    }
}
