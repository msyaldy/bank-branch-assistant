namespace BranchScribe.Services.Interfaces;

using BranchScribe.Models;

public interface ILocalLLM
{
    string ProviderId { get; }
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task MakeAvailableAsync(CancellationToken ct = default);
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default);
    IAsyncEnumerable<string> CompleteStreamingAsync(LlmRequest request, CancellationToken ct = default);
}
