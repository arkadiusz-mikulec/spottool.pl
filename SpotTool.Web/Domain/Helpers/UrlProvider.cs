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

    public static class Spot
    {
        public const string SpotPath = "spots";
        public const string SpotActive = SpotPath + "/active";
        
        public static class LinkAndIcon
        {
            public readonly static (string Text, string Icon) Value = ("Spots", MudBlazor.Icons.Material.Filled.Monitor);
        }
    }

    public static class Monitoring
    {
        public const string MonitoringPath = "monitoring";
        public const string MonitoringPathWithIssuerIdAndItemId = MonitoringPath + "/{issuer:guid?}/{id:int?}";

        public static class LinkAndIcon
        {
            public readonly static (string Text, string Icon) Value = ("Monitoring", MudBlazor.Icons.Material.Filled.Monitor);
        }
    }

    public static class Lead
    {
        public const string LeadPath = "lead";
        public const string AddLeadPath = LeadPath + "/{name}/{email}/{mobile}/{operation}/{ts}/add";
        public const string RedirectToYtPath = LeadPath + "/{operation}/{ts}/{platform}/redirect";

    }

}