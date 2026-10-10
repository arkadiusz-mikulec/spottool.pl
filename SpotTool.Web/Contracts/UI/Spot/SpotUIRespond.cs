using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;

namespace SpotTool.Web.Contracts.UI.Spot;

public class SpotUIRespond
{
    public SpotUIRespond(DbModels.Spot spot, Role.User userRoleForThisSpot)
{
        Spot = spot;

        if(userRoleForThisSpot == Role.User.Bidder)
        {
            StatusDescription = "Carrier status";
            SpotButtons.AddRange([Buttons.AddOfferButton()]);
        }
        else
        {
            StatusDescription = spot.CurrentStatus.ToString();
            
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
    public string StatusDescription { get; set; } = string.Empty;
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