using SpotTool.Web.Contracts.UI.Offer;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;

namespace SpotTool.Web.Contracts.UI.Spot;

public class SpotUIRespond
{
    public SpotUIRespond(DbModels.Spot spot, Role.User userRoleForThisSpot, LastBidderOfferRespond? lastBidderOfferRespond = null)
{
        Spot = spot;
        StatusUIRespond = new SpotStatusUIRespond(spot.Id, spot.CurrentStatus, userRoleForThisSpot, lastBidderOfferRespond);
            
        if(userRoleForThisSpot == Role.User.Bidder)
        {
            SpotButtons.AddRange([Buttons.AddOfferButton()]);
        }
        else
        {
            List<Button> defaultButtons = [Buttons.EditButton(), Buttons.DetailsButton(), Buttons.DeleteButton()];
            if(spot.CurrentStatus == Status.Spot.NotConfirmed)
            {
                SpotButtons.Add(Buttons.ConfirmButton());
                SpotButtons.AddRange(defaultButtons);
            }
            else 
                SpotButtons.AddRange(defaultButtons);
        }
    }
    public DbModels.Spot Spot {get;set;} 
    public SpotStatusUIRespond StatusUIRespond { get; set; }
    public List<Button> SpotButtons {get; set;} = [];
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
    public static Button DetailsButton()
    { 
        return new Button {ButtonText = "Details", ButtonIcon = "d"}; 
    }
    public static Button DeleteButton()
    { 
        return new Button {ButtonText = "Delete", ButtonIcon = "d"}; 
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

public class SpotStatusUIRespond(Guid spotId, Status.Spot spotStatus, Role.User userRole, LastBidderOfferRespond? lastBidderOffer)
{ 
    public Guid SpotId {get;} = spotId;
    public  Status.Spot SpotStatus {get;} = spotStatus;
    public Role.User UserRole {get;} = userRole;
    private LastBidderOfferRespond? LastBidderOffer {get;} = lastBidderOffer;
    public (MudBlazor.Color color, string text) GetStatusProperties()
    {
        //StatusUIChipsLook.Color = MudBlazor.Color.Primary;
        if(LastBidderOffer is null)
            return (StatusUIChipsLook.Color, Status.SpotName.Get(SpotStatus, UserRole));

        MudBlazor.Color color = LastBidderOffer!.OfferValueStatus switch
        {
            Status.OfferValue.Green => MudBlazor.Color.Success,
            Status.OfferValue.Yellow => MudBlazor.Color.Warning,
            _ => MudBlazor.Color.Error
        };
        string text = $"{Status.SpotName.Get(SpotStatus, UserRole)} {LastBidderOffer.Offer}";
        return (color, text);
    }
}

public static class StatusUIChipsLook
{
    public static MudBlazor.Color Color {get; set;} = MudBlazor.Color.Dark;
}