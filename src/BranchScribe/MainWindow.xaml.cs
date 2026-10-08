namespace BranchScribe;

using Microsoft.UI.Xaml;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        RootFrame.Navigate(typeof(Views.ShellPage));
    }
}
