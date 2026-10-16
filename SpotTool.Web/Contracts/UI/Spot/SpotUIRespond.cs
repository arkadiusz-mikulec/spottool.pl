using Marten.Linq.Parsing;
using SpotTool.Web.Contracts.UI.Offer;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Contracts.UI.Spot;

public class SpotUIRespond
{
    public SpotUIRespond(SpotModel spotModel, Guid UserId, Role.User userRoleForThisSpot)
{
        SpotModel = spotModel;
        StatusUIRespond = new SpotStatusUIRespond(spotModel.Spot.Id, UserId, spotModel.Spot.CurrentStatus, userRoleForThisSpot, spotModel.Offers);
            
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

public class SpotStatusUIRespond(Guid SpotId, Guid UserId, Status.Spot SpotStatus, Role.User UserRole, List<DbModels.Offer>? Offers)
{ 
    public Guid SpotId {get;} = SpotId;
    public Status.Spot CurrentSpotStatus { get; } = SpotStatus;

    public (MudBlazor.Color color, string text, string style) GetStatusProperties()
    {
        DbModels.Offer? minOffer = null; 
        string textStyle = string.Empty;
        string statusInfo = string.Empty;
        string statusName = Status.GetStatusName(CurrentSpotStatus, UserRole); 
        MudBlazor.Color color = MudBlazor.Color.Dark;

        if(Offers is not null && Offers.Count > 0)
        {    
            if(UserRole == Role.User.Issuer)
            {
                minOffer = Offers.Where(m=>m.CreatedByUser.Id != UserId).MinBy(m=>m.Value);
                color = DefineColor(minOffer);
                textStyle = minOffer?.Status == Status.Offer.Rejected ? "text-decoration: line-through;" : "";    
                
                if(CurrentSpotStatus == Status.Spot.Negotiation)
                {
                    var negOffers = Offers.Where(m=>m.Status == Status.Offer.ToNegotiation);

                    statusInfo = $"{negOffers.Count()} {statusName}";
                }
                else
                {
                    statusInfo = $"{statusName} {minOffer?.Value} [{minOffer?.CurrencyCode}]";    
                }
            }
            else
            {
                var carrierMinOffer = Offers.Where(m=>m.CreatedForUser.Id == UserId).MinBy(m=>m.Value);
                if(CurrentSpotStatus == Status.Spot.Negotiation)
                {
                    var negOffer = Offers.FirstOrDefault(m=>m.CreatedForUser.Id == UserId && m.Status == Status.Offer.ToNegotiation);
                    if(negOffer is not null)
                    {
                        statusInfo = $"{statusName} for {negOffer.Value} [{negOffer.CurrencyCode}]";
                        color = DefineColor(negOffer);    
                    }
                    else
                    {
                        if(carrierMinOffer?.CreatedForUser.Id == UserId)
                        {
                            textStyle = carrierMinOffer?.Status == Status.Offer.Rejected ? "text-decoration: line-through;" : "";
                            statusInfo = $"{carrierMinOffer?.Value} [{carrierMinOffer?.CurrencyCode}]";
                        }
                        else
                            statusInfo = $"{statusName}";

                        color = DefineColor(carrierMinOffer);        
                    }
                }
                else
                {
                    if(carrierMinOffer?.CreatedForUser.Id == UserId)
                    {
                        textStyle = carrierMinOffer?.Status == Status.Offer.Rejected ? "text-decoration: line-through;" : "";
                        statusInfo = $"{carrierMinOffer?.Value} [{carrierMinOffer?.CurrencyCode}]";
                    }
                    else
                        statusInfo = $"{statusName}";    

                    color = DefineColor(carrierMinOffer);
                }
            }
        }
        else
            statusInfo = statusName;

        return (color, statusInfo, textStyle); 
    }

    private MudBlazor.Color DefineColor(DbModels.Offer? minOffer)
    {
        MudBlazor.Color color;
        
        if(UserRole == Role.User.Issuer || UserRole == Role.User.Admin)
        {
            switch(CurrentSpotStatus)
            {
                case Status.Spot.Accepted:
                case Status.Spot.Ok:
                    color = MudBlazor.Color.Success;        
                break;
                case Status.Spot.Bidding:
                case Status.Spot.Negotiation:
                    color = MudBlazor.Color.Info;
                break;
                case Status.Spot.Canceled:
                    color = MudBlazor.Color.Error;
                break;
                default: 
                    color = MudBlazor.Color.Dark;
                break;    
            }
        }
        else
        {
            switch(CurrentSpotStatus)
            {
                case Status.Spot.Ok:
                case Status.Spot.Accepted:
                    color = MudBlazor.Color.Success;        
                break;
                case Status.Spot.Bidding:
                    {
                        if(minOffer is not null)
                        {
                            if(minOffer.ValueStatus == Status.OfferValue.Green)
                                color = MudBlazor.Color.Success;
                            else if(minOffer.ValueStatus == Status.OfferValue.Yellow)
                                color = MudBlazor.Color.Warning;
                            else
                                color = MudBlazor.Color.Error;
                        }
                        else
                            color = MudBlazor.Color.Info;
                    }
                break;
                case Status.Spot.Negotiation:
                    {
                        if(minOffer is not null && minOffer.CreatedForUser.Id == UserId && minOffer.Status == Status.Offer.ToNegotiation)
                            color = MudBlazor.Color.Info;
                        else
                            color = MudBlazor.Color.Error;    
                    }
                break;
                case Status.Spot.Canceled:
                case Status.Spot.EndedCostOK:
                case Status.Spot.EndedCostTooHigh:
                    color = MudBlazor.Color.Error;
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