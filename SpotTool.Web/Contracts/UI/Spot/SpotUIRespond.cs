using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;

namespace SpotTool.Web.Contracts.UI.Spot;

public class SpotUIRespond
{
    public SpotUIRespond(DbModels.Spot spot)
{
        Spot = spot;
        DispoStatusDescription = spot.CurrentStatus.ToString();
        CarrierStatusDescription = "Carrier status";

        if(spot.CurrentStatus == Status.Spot.NotConfirmed)
            SpotDispoButtons.AddRange([Buttons.ConfirmButton()]);
        else if(spot.CurrentStatus == Status.Spot.Ok || spot.CurrentStatus == Status.Spot.DuringBidding)
            SpotDispoButtons.AddRange([Buttons.EditButton()]);
        
        SpotCarrierButtons.AddRange([Buttons.AddOfferButton()]);
    
    }
    public DbModels.Spot Spot {get;set;} 
    public string CarrierStatusDescription { get; set; } = string.Empty;
    public string DispoStatusDescription { get; set; } = string.Empty;
    public List<Button> SpotCarrierButtons {get; set;} = [];
    public List<Button> SpotDispoButtons {get; set;} = [];
}

public static class Buttons
{
    public static Button ConfirmButton()
    { 
        return new Button {ButtonText = "Confirm", ButtonIcon = "edit"}; 
    }
    public static Button EditButton()
    { 
        return new Button {ButtonText = "Edit", ButtonIcon = "edit"}; 
    }
    public static Button AddOfferButton()
    { 
        return new Button {ButtonText = "Add offer", ButtonIcon = "edit"}; 
    }   
}

public class Button
{
    public string ButtonText { get; set; } = string.Empty;
    public string ButtonIcon { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
}