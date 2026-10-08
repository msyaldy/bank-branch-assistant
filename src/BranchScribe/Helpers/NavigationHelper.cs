namespace BranchScribe.Helpers;

public static class NavigationHelper
{
    public static Type? GetPageType(string tag) => tag switch
    {
        "home" => typeof(Views.HomePage),
        "session" => typeof(Views.SessionPage),
        "reviewpack" => typeof(Views.ReviewPackPage),
        "insights" => typeof(Views.InsightsPage),
        _ => null,
    };
}
