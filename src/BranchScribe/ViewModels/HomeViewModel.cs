namespace BranchScribe.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    private string _advisorName = string.Empty;

    [ObservableProperty]
    private string _greeting = string.Empty;

    [ObservableProperty]
    private string _todayDate = string.Empty;

    [ObservableProperty]
    private int _appointmentCount;

    [ObservableProperty]
    private int _gapCount;

    [ObservableProperty]
    private int _prepItemCount;

    public ObservableCollection<AppointmentItem> Appointments { get; } = [];
    public ObservableCollection<PrepItem> PrepItems { get; } = [];
    public ObservableCollection<string> CalendarGaps { get; } = [];
}

public class AppointmentItem
{
    public string Time { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusColor { get; set; } = "#00A676";
    public bool IsGap { get; set; }
}

public class PrepItem
{
    public string Icon { get; set; } = "\uE8A5";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
    public string PriorityColor { get; set; } = "#F59E0B";
}
