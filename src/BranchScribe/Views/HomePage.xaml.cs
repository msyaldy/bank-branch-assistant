namespace BranchScribe.Views;

using BranchScribe.Services.Interfaces;
using BranchScribe.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }
    private readonly IPrepGenerator _prepGenerator;

    public HomePage()
    {
        ViewModel = App.Current.Services.GetRequiredService<HomeViewModel>();
        _prepGenerator = App.Current.Services.GetRequiredService<IPrepGenerator>();
        this.InitializeComponent();

        if (ViewModel.Appointments.Count == 0)
            LoadSampleData();
    }

    private void LoadSampleData()
    {
        var hour = DateTime.Now.Hour;
        ViewModel.AdvisorName = "Laura";
        ViewModel.Greeting = hour < 12 ? $"Good morning, {ViewModel.AdvisorName}"
                           : hour < 17 ? $"Good afternoon, {ViewModel.AdvisorName}"
                           : $"Good evening, {ViewModel.AdvisorName}";
        ViewModel.TodayDate = DateTime.Now.ToString("dddd, d MMMM yyyy");

        // Today's appointments
        var appointments = new AppointmentItem[]
        {
            new() { Time = "09:00", CustomerName = "Sarah Mitchell", Type = "Mortgage", Status = "Completed", StatusColor = "#00A676" },
            new() { Time = "09:45", CustomerName = "— Available —", Type = "", Status = "Gap", StatusColor = "#F59E0B", IsGap = true },
            new() { Time = "10:30", CustomerName = "James Cooper", Type = "ISA Transfer", Status = "Prep needed", StatusColor = "#EF4444" },
            new() { Time = "11:15", CustomerName = "Emily Watson", Type = "Joint Account", Status = "Ready", StatusColor = "#00A676" },
            new() { Time = "12:00", CustomerName = "— Lunch —", Type = "", Status = "Break", StatusColor = "#6B7280", IsGap = true },
            new() { Time = "13:00", CustomerName = "David Chen", Type = "Business Loan", Status = "Prep needed", StatusColor = "#EF4444" },
            new() { Time = "14:00", CustomerName = "— Available —", Type = "", Status = "Gap", StatusColor = "#F59E0B", IsGap = true },
            new() { Time = "14:45", CustomerName = "Rachel Adams", Type = "Remortgage", Status = "Ready", StatusColor = "#00A676" },
            new() { Time = "15:30", CustomerName = "Michael Brown", Type = "Savings Review", Status = "Confirmed", StatusColor = "#3B82F6" },
        };

        foreach (var a in appointments)
            ViewModel.Appointments.Add(a);

        // Calendar gaps
        ViewModel.CalendarGaps.Add("09:45 – 10:30 — 45 min gap. Could fit a walk-in or admin catch-up.");
        ViewModel.CalendarGaps.Add("14:00 – 14:45 — 45 min gap. Consider outbound follow-up calls.");

        ViewModel.AppointmentCount = appointments.Count(a => !a.IsGap);
        ViewModel.GapCount = 2;

        // Keep the panel populated while the first local inference warms up.
        ViewModel.PrepItems.Add(new PrepItem
        {
            Icon = "\uE945",
            Title = "Preparing local insights",
            Description = "The QNN model is checking today’s appointments on the local NPU.",
            Priority = "Running locally",
            PriorityColor = "#0B5CAD",
        });
        ViewModel.PrepItemCount = 1;

        // Generate prep items using on-device Phi AI
        _ = Task.Run(async () =>
        {
            try
            {
                var prepItems = await _prepGenerator.GeneratePrepItemsAsync(appointments);
                DispatcherQueue.TryEnqueue(() =>
                {
                    ViewModel.PrepItems.Clear();
                    foreach (var item in prepItems)
                        ViewModel.PrepItems.Add(item);
                    ViewModel.PrepItemCount = prepItems.Count;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Prep] AI generation failed: {ex.Message}");
                DispatcherQueue.TryEnqueue(() =>
                {
                    ViewModel.PrepItems.Clear();
                    ViewModel.PrepItems.Add(new PrepItem
                    {
                        Icon = "\uE7BA",
                        Title = "AI Prep Unavailable",
                        Description = "On-device Phi model could not generate prep items. Check Windows AI availability.",
                        Priority = "Low",
                        PriorityColor = "#6B7280",
                    });
                    ViewModel.PrepItemCount = 1;
                });
            }
        });
    }
}
