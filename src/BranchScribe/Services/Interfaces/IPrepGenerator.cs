namespace BranchScribe.Services.Interfaces;

using BranchScribe.ViewModels;

public interface IPrepGenerator
{
    Task<IReadOnlyList<PrepItem>> GeneratePrepItemsAsync(
        IReadOnlyList<AppointmentItem> appointments,
        CancellationToken ct = default);
}
