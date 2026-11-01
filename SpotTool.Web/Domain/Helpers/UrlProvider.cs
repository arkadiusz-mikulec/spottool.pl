namespace SpotTool.Web.Domain.Helpers;

public static class UrlProvider
{
    public static class Home
    {
        public const string HomePath = "/";

        public static class LinkAndIcon
        {
            public static (string Text, string Icon) Home = ("Home", "home");
        }
    }

    public static class Monitoring
    {
        public const string MonitoringPath = "monitoring";
        public const string MonitoringPathWithUserAndId = MonitoringPath + "/{user:guid?}/{id:int?}";

        public static class LinkAndIcon
        {
            public readonly static (string Text, string Icon) Value = ("Monitoring", MudBlazor.Icons.Material.Filled.Monitor);
        }
    }


}