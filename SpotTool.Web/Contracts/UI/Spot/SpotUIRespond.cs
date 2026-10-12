using SpotTool.Web.Contracts.UI.Offer;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Contracts.UI.Spot;

public class SpotUIRespond
{
    public SpotUIRespond(SpotModel spotModel, Role.User userRoleForThisSpot)
{
        SpotModel = spotModel;
        StatusUIRespond = new SpotStatusUIRespond(spotModel.Spot.Id, spotModel.Spot.CurrentStatus, userRoleForThisSpot, spotModel.Offers);
            
        if(userRoleForThisSpot == Role.User.Bidder)
        {
            SpotButtons.AddRange([Buttons.AddOfferButton(), Buttons.DetailsButton()]);
        }
        else
        {
            List<Button> defaultButtons = [Buttons.EditButton(), Buttons.DetailsButton(), Buttons.DeleteButton()];
            if(spotModel.Spot.CurrentStatus == Status.Spot.NotConfirmed)
            {
                SpotButtons.Add(Buttons.ConfirmButton());
                SpotButtons.AddRange(defaultButtons);
            }
            else 
                SpotButtons.AddRange(defaultButtons);
        }
    }
    public SpotModel SpotModel {get;set;} 
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

public class SpotStatusUIRespond(Guid SpotId, Status.Spot SpotStatus, Role.User UserRole, List<DbModels.Offer>? Offers)
{ 
    public Guid SpotId {get;} = SpotId;

    public (MudBlazor.Color color, string text, string style) GetStatusProperties()
    {
        var statusInfo = Status.GetStatusName(SpotStatus, UserRole);
        DbModels.Offer? minOffer = null; 
        string textStyle = string.Empty;

        if(Offers is not null)
        {
            minOffer = Offers.MinBy(m=>m.Value);
            statusInfo += $" {minOffer?.Value} [{minOffer?.CurrencyCode}]";
            if(minOffer?.Status == Status.Offer.Rejected)
                textStyle = "text-decoration: line-through;";
        }
        return (DefineColor(minOffer), statusInfo, textStyle); 
    }

    private MudBlazor.Color DefineColor(DbModels.Offer? minOffer)
    {
        MudBlazor.Color color;
        
        if(UserRole == Role.User.Issuer || UserRole == Role.User.Admin)
        {
            switch(SpotStatus)
            {
                case Status.Spot.Accepted:
                case Status.Spot.Ok:
                    color = MudBlazor.Color.Success;        
                break;
                case Status.Spot.Bidding:
                    color = MudBlazor.Color.Info;
                break;
                default: 
                    color = MudBlazor.Color.Dark;
                break;    
            }
        }
        else
        {
            switch(SpotStatus)
            {
                case Status.Spot.Accepted:
                    color = MudBlazor.Color.Success;        
                break;
                case Status.Spot.Bidding:
                    color = minOffer is not null ? 
                                (minOffer.ValueStatus == Status.OfferValue.Green ? 
                                    MudBlazor.Color.Success : 
                                    minOffer.ValueStatus == Status.OfferValue.Yellow ? 
                                        MudBlazor.Color.Warning : 
                                        MudBlazor.Color.Error) :
                                MudBlazor.Color.Info;
                break;
                default: 
                    color = MudBlazor.Color.Dark;
                break;    
            }
        }

        return color;
    }
}

public record SpotDeadlineUpdateRespond(Guid SpotId, DateTimeOffset DeadLine);

// public class SpotStatusUIRespond(Guid spotId, Status.Spot spotStatus, Guid userId, Role.User userRole, List<DbModels.Offer>? offers)
// { 
//     public Guid SpotId {get;} = spotId;
//     public  Status.Spot SpotStatus {get;} = spotStatus;
//     public Role.User UserRole {get;} = userRole;
//     private List<DbModels.Offer>? Offer {get;} = offers;
//     public (MudBlazor.Color color, string text) GetStatusProperties()
//     {
//         MudBlazor.Color color;
//         if(Offer is null || Offer.Count == 0)
//         {
//             color = SpotStatus switch
//                 {
//                     Status.Spot.Ok => MudBlazor.Color.Success,
//                     _ => MudBlazor.Color.Dark
//                 };
//             return (color, Status.SpotName.Get(SpotStatus, UserRole)); 
//         }
//         else
//         {
//             color = MudBlazor.Color.Info;
//             var lastOffer = Offer.OrderBy(m=>m.Value).First();
                
//             if(UserRole == Role.User.Bidder)
//             {
//                 lastOffer = Offer.Where(m=>m.CreatedForUser.Id == userId)
//                     .OrderBy(m=>m.Value).First();

//                 color = lastOffer.ValueStatus switch
//                 {
//                     Status.OfferValue.Green => MudBlazor.Color.Success,
//                     Status.OfferValue.Yellow => MudBlazor.Color.Warning,
//                     _ => MudBlazor.Color.Error
//                 };    
//             }
//             string text = $"{Status.SpotName.Get(SpotStatus, UserRole)} {lastOffer.Value} [{lastOffer.CurrencyCode}]";
//             return (color, text);    
//         }
//     }
// }

public static class StatusUIChipsLook
{
    public static MudBlazor.Color Color {get; set;} = MudBlazor.Color.Dark;
}