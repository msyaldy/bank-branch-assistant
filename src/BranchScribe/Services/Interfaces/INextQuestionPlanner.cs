namespace BranchScribe.Services.Interfaces;

using BranchScribe.Models;

public interface INextQuestionPlanner
{
    Task<string> GetNextQuestionAsync(FormSchema schema, ExtractionResult currentExtraction, CancellationToken ct = default);
    Task<List<string>> GetMissingQuestionsAsync(FormSchema schema, ExtractionResult currentExtraction, CancellationToken ct = default);
}
