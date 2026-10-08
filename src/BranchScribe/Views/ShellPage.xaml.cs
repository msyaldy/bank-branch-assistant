namespace BranchScribe.Views;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

public sealed partial class ShellPage : Page
{
    private Button? _activeNavButton;
    private Border? _activeAccentBorder;

    public ShellPage()
    {
        this.InitializeComponent();
        // Default to Home page with active state
        _activeNavButton = NavHome;
        _activeAccentBorder = NavHomeAccent;
        ContentFrame.Navigate(typeof(HomePage));
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            var tag = button.Tag?.ToString();
            var pageType = Helpers.NavigationHelper.GetPageType(tag ?? string.Empty);
            if (pageType is not null)
            {
                ContentFrame.Navigate(pageType);
                SetActiveNav(button, tag);
            }
        }
    }

    private void SetActiveNav(Button clicked, string? tag)
    {
        // Reset previous active button
        if (_activeNavButton is not null)
            _activeNavButton.Style = (Style)Application.Current.Resources["NavButtonStyle"];
        if (_activeAccentBorder is not null)
            _activeAccentBorder.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        // Apply active style to clicked button
        clicked.Style = (Style)Application.Current.Resources["NavButtonActiveStyle"];
        _activeNavButton = clicked;

        // Set accent border
        Border? accent = tag switch
        {
            "home" => NavHomeAccent,
            "session" => NavSessionAccent,
            "reviewpack" => NavReviewPackAccent,
            "insights" => NavInsightsAccent,
            _ => null
        };

        if (accent is not null)
        {
            accent.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 166, 118)); // #00A676
            _activeAccentBorder = accent;
        }
    }
}
