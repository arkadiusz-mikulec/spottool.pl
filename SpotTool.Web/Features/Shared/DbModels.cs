namespace SpotTool.Web.Feature.Shared;


public class Spot
{
    public Guid Id {get; set;}
    public string? Route {get; set;}
    public decimal Price {get; set;}
    public DateTime CreatedAt {get; set;} = DateTime.UtcNow.ToLocalTime();
}
    