namespace BranchScribe.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using BranchScribe.ViewModels;
using BranchScribe.Models;

public sealed partial class ReviewPackPage : Page
{
    public ReviewPackViewModel ViewModel { get; }

    public ReviewPackPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<ReviewPackViewModel>();
        // Only load sample data if no real pack has been generated
        if (ViewModel.Pack is null && string.IsNullOrEmpty(ViewModel.AdvisorSummary) ||
            ViewModel.AdvisorSummary == "No pack generated yet. Complete a session first.")
            LoadSampleData();
        this.InitializeComponent();
    }

    private void LoadSampleData()
    {
        var pack = new Pack
        {
            SessionId = "demo-session-001",
            CustomerName = "Sarah Mitchell",
            AdvisorSummary = "Meeting with Sarah Mitchell regarding a first-time buyer mortgage application. "
                + "Customer is employed full-time as a software engineer with an annual income of £62,000. "
                + "Looking to purchase a property valued at approximately £285,000 with a 10% deposit (£28,500). "
                + "Discussed fixed-rate preferences and existing credit commitments. Customer expressed interest "
                + "in a 5-year fixed rate. Identity verification completed — passport and utility bill provided. "
                + "Next steps: formal affordability assessment, property valuation, and solicitor instruction.",

            CustomerSummary = "Dear Sarah,\n\nThank you for meeting with us today to discuss your mortgage application.",

            CapturedFieldsSummary = "  • customer name: Sarah Jane Mitchell\n" +
                                    "  • date of birth: 15th March 1992\n" +
                                    "  • annual income: £62,000\n" +
                                    "  • employment status: Employed full-time\n" +
                                    "  • employer name: Contoso Ltd\n" +
                                    "  • property value range: £285,000\n" +
                                    "  • deposit amount: £28,500\n" +
                                    "  • rate preference: 5-year fixed rate",

            ActionChecklist =
            [
                "Submit formal mortgage application to lender",
                "Arrange independent property valuation",
                "Request solicitor details from customer",
                "Complete AML / KYC checks",
                "Send customer summary letter via secure post",
                "Schedule 2-week follow-up call",
            ],

            MissingConfirmations =
            [
                "Monthly living expenses",
                "Partner annual income",
                "Credit score check consent",
            ],

            FieldCitations = new Dictionary<string, List<Citation>>
            {
                ["customer_name"] = [new Citation { SegmentIndex = 0, Timestamp = TimeSpan.FromSeconds(12), Snippet = "Sarah Jane Mitchell, born 15th March 1992" }],
                ["annual_income"] = [new Citation { SegmentIndex = 4, Timestamp = TimeSpan.FromSeconds(50), Snippet = "Sixty-two thousand pounds" }],
                ["property_value_range"] = [new Citation { SegmentIndex = 6, Timestamp = TimeSpan.FromSeconds(85), Snippet = "two-bed flat in Reading, listed at two hundred and eighty-five thousand" }],
                ["deposit_amount"] = [new Citation { SegmentIndex = 7, Timestamp = TimeSpan.FromSeconds(100), Snippet = "Twenty-eight thousand five hundred — so about ten percent" }],
                ["rate_preference"] = [new Citation { SegmentIndex = 10, Timestamp = TimeSpan.FromSeconds(118), Snippet = "I'd prefer a five-year fixed rate for stability" }],
            },
        };

        ViewModel.LoadPack(pack);
    }
}
