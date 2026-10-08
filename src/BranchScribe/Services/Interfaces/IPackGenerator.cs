namespace BranchScribe.Services.Interfaces;

using BranchScribe.Models;

public interface IPackGenerator
{
    Task<Pack> GenerateAsync(Session session, FormSchema schema, ExtractionResult extraction, CancellationToken ct = default);
}
