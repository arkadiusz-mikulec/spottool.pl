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
            if(spotModel.Spot.CurrentStatus == Status.Spot.Ok || spotModel.Spot.CurrentStatus == Status.Spot.Bidding)
                SpotButtons.Add(Buttons.AddOfferButton());

            if(spotModel.Spot.CurrentStatus == Status.Spot.Negotiation)
                SpotButtons.Add(Buttons.RejectOfferButton());
        }
        else
        {
            List<Button> defaultButtons = [Buttons.EditButton(), Buttons.CopyButton(), Buttons.DeleteButton()];
            if(spotModel.Spot.CurrentStatus == Status.Spot.NotConfirmed)
            {
                SpotButtons.Add(Buttons.ConfirmButton());
                SpotButtons.AddRange(defaultButtons);
            }
            else 
                SpotButtons.AddRange(defaultButtons);
        }
    }
    // SpotmMdel to record
    public SpotModel SpotModel {get;set;} 
    // SpotStatusUIRespond to klasa
    public SpotStatusUIRespond StatusUIRespond { get; set; }
    //Button to statyczna klasa
    public List<Button> SpotButtons {get; set;} = [];
}

public enum ButtonAction
{
    CONFIRM_SPOT,
    EDIT_SPOT,
    DELETE_SPOT,
    COPY_SPOT,
    ADD_OFFER,
    DELETE_OFFER,
    REJECT_OFFER,
    DETAILS
}
public static class Buttons
{
    public static Button CopyButton()
    { 
        return new Button {ActionName = ButtonAction.COPY_SPOT, ButtonText = "CoPY", ButtonIcon = MudBlazor.Icons.Material.Filled.CopyAll, Color = MudBlazor.Color.Secondary }; 
    }
    public static Button ConfirmButton()
    { 
        return new Button {ActionName = ButtonAction.CONFIRM_SPOT, ButtonText = "Confirm", ButtonIcon = MudBlazor.Icons.Material.Filled.Approval, Color = MudBlazor.Color.Dark }; 
    }
    public static Button EditButton()
    { 
        return new Button {ActionName = ButtonAction.EDIT_SPOT, ButtonText = "Edit", ButtonIcon = MudBlazor.Icons.Material.Filled.Edit, Color = MudBlazor.Color.Primary}; 
    }
    public static Button DetailsButton()
    { 
        return new Button {ActionName = ButtonAction.DETAILS, ButtonText = "Details", ButtonIcon = MudBlazor.Icons.Material.Filled.Details, Color = MudBlazor.Color.Info }; 
    }
    public static Button DeleteButton()
    { 
        return new Button { ActionName = ButtonAction.DELETE_SPOT, ButtonText = "Delete", ButtonIcon = MudBlazor.Icons.Material.Filled.Delete, Color = MudBlazor.Color.Error }; 
    }

    public static Button DeleteOfferButton()
    { 
        return new Button {ActionName = ButtonAction.DELETE_OFFER, ButtonText = "Delete", ButtonIcon = MudBlazor.Icons.Material.Filled.Delete, Color = MudBlazor.Color.Error }; 
    }
    
    public static Button AddOfferButton()
    { 
        return new Button {ActionName = ButtonAction.ADD_OFFER, ButtonText="Add offer", ButtonIcon = MudBlazor.Icons.Material.Filled.Add, Color = MudBlazor.Color.Primary }; 
    }
    public static Button RejectOfferButton()
    { 
        return new Button {ActionName = ButtonAction.REJECT_OFFER, ButtonText="Reject neg", ButtonIcon = MudBlazor.Icons.Material.Filled.Cancel, Color = MudBlazor.Color.Info }; 
    }
 
}
public class Button
{
    public ButtonAction ActionName {get; set;}
    public string ButtonText { get; set; } = string.Empty;
    public string? ButtonIcon { get; set; }
    public bool IsVisible { get; set; } = true;
    public MudBlazor.Color Color { get; set; }
    //public EventCallback Callback {get; set;}
}

public class SpotStatusUIRespond(Guid SpotId, Guid UserId, Status.Spot SpotStatus, Role.User UserRole, List<DbModels.Offer>? Offers)
{ 
    public Guid SpotId {get;} = SpotId;
    public Status.Spot CurrentSpotStatus { get; } = SpotStatus;
    //public List<DbModels.Offer>? Offers { get; } = Offers;
    //public Guid UserId { get; } = UserId;

    public (MudBlazor.Color color, string text, string style) GetStatusProperties()
    {
        //DbModels.Offer? minOffer = null; 
        string textStyle = string.Empty; 

        var statusToPresent = Status.GetStatusName(CurrentSpotStatus, UserRole);
        string statusInfo = statusToPresent.ToString().ToUpper();
        MudBlazor.Color color = DefineColor(statusToPresent);

        if(UserRole == Role.User.Issuer)
        {
            switch(CurrentSpotStatus)
            {
                case Status.Spot.Accepted:
                {
                    var offerAccepted = Offers?.FirstOrDefault(m=>m.IsWinnerOffer);
                    statusInfo += $" {offerAccepted?.Value} [{offerAccepted?.CurrencyCode}] - {offerAccepted?.CreatedByUser.PersonDetail.Email}";    
                }
                break;
                case Status.Spot.Bidding:
                case Status.Spot.Ended:
                {
                    var minOffer = Offers?.Where(m=>m.CreatedByUser.Id != UserId).MinBy(m=>m.Value);
                    textStyle = minOffer?.Status == Status.Offer.Rejected ? "text-decoration: line-through;" : "";    
                    statusInfo += $" {minOffer?.Value} [{minOffer?.CurrencyCode}]";        
                }
                break;
                case Status.Spot.Negotiation:
                {
                    var negOffers = Offers?.Where(m=>m.Status == Status.Offer.ToNegotiation).ToList();
                    statusInfo += $" X {negOffers?.Count}";    
                }
                break;
            }
        }
        else
        {
            switch(CurrentSpotStatus)
            {
                case Status.Spot.Accepted:
                {
                    var offerAccepted = Offers?.FirstOrDefault(m=>m.IsWinnerOffer && m.CreatedForUser.Id == UserId);
                    if(offerAccepted is not null)
                    {    
                        statusInfo += $" {offerAccepted.Value} [{offerAccepted.CurrencyCode}]";
                        color = DefineColor(statusToPresent);
                    }
                    else
                    {
                        statusInfo = $"{Status.Spot.Rejected}".ToUpper();
                        color = DefineColor(Status.Spot.Rejected);
                    }    
                }
                break;
                case Status.Spot.Bidding:
                case Status.Spot.Ended:
                {
                    string statusText = CurrentSpotStatus == Status.Spot.Ended ? $"{CurrentSpotStatus}".ToUpper() : "";
                    var carrierMinOffer = Offers?.Where(m=>m.CreatedForUser.Id == UserId && m.CreatedByUser.Id == UserId).MinBy(m=>m.Value);
                    if(carrierMinOffer?.CreatedForUser.Id == UserId)
                    {
                        textStyle = carrierMinOffer?.Status == Status.Offer.Rejected ? "text-decoration: line-through;" : "";
                        statusInfo = $"{statusText} {carrierMinOffer?.Value} [{carrierMinOffer?.CurrencyCode}]";
                        color = DefineColor(CurrentSpotStatus, carrierMinOffer);
                    }
                    else
                    {
                        statusInfo = $"{CurrentSpotStatus}".ToUpper();
                        color = DefineColor(CurrentSpotStatus);    
                    }
                }
                break;
                case Status.Spot.Negotiation:
                {
                    var negOffer = Offers?.FirstOrDefault(m=>m.CreatedForUser.Id == UserId && m.Status == Status.Offer.ToNegotiation);
                    if(negOffer is not null)
                    {
                        statusInfo = $"{statusToPresent} for {negOffer.Value} [{negOffer.CurrencyCode}]";    
                        color = DefineColor(statusToPresent, negOffer);
                    }
                    else
                    {
                        var carrierMinOffer = Offers?.Where(m=>m.CreatedForUser.Id == UserId && m.CreatedByUser.Id == UserId).MinBy(m=>m.Value);
                        if(carrierMinOffer?.CreatedForUser.Id == UserId)
                        {
                            textStyle = carrierMinOffer?.Status == Status.Offer.Rejected ? "text-decoration: line-through;" : "";
                            statusInfo = $"{Status.Spot.Ended} {carrierMinOffer?.Value} [{carrierMinOffer?.CurrencyCode}]".ToUpper();
                            color = DefineColor(Status.Spot.Ended, carrierMinOffer);
                        }
                        else
                        {
                            color = DefineColor(Status.Spot.Ended);    
                            statusInfo = $"NIE POWINNO BYC - {Status.Spot.Ended}";
                        }
                    }    
                }
                break;
            }
        }

        return (color, statusInfo, textStyle); 
    }

    private MudBlazor.Color DefineColor(Status.Spot statusToPresent, DbModels.Offer? offer = null)
    {
        MudBlazor.Color color;
        
        if(UserRole == Role.User.Issuer)
        {
            switch(statusToPresent)
            {
                case Status.Spot.Accepted:
                case Status.Spot.Ok:
                    color = MudBlazor.Color.Success;  
                break;
                case Status.Spot.Ended:
                    {
                        if(offer is not null && offer.DeltaToMinOffer <= 0)
                            color = MudBlazor.Color.Success;
                        else
                            color = MudBlazor.Color.Error;
                    }    
                break;
                case Status.Spot.Bidding:
                case Status.Spot.Negotiation:
                    color = MudBlazor.Color.Info;
                break;
                case Status.Spot.Canceled:
                case Status.Spot.Deleted:
                case Status.Spot.EndedNoOffer:
                case Status.Spot.Rejected:
                    color = MudBlazor.Color.Error;
                break;
                default: 
                    color = MudBlazor.Color.Dark;
                break;    
            }
        }
        else
        {
            switch(statusToPresent)
            {
                case Status.Spot.Ok:
                case Status.Spot.Accepted:
                    color = MudBlazor.Color.Success;        
                break;
                case Status.Spot.Bidding:
                    {
                        if(offer is not null)
                        {
                            if(offer.ValueStatus == Status.OfferValue.Green)
                                color = MudBlazor.Color.Success;
                            else if(offer.ValueStatus == Status.OfferValue.Yellow)
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
                        if(offer is not null && offer.CreatedForUser.Id == UserId && offer.Status == Status.Offer.ToNegotiation)
                            color = MudBlazor.Color.Info;
                        else
                            color = MudBlazor.Color.Error;    
                    }
                break;
                case Status.Spot.Canceled:
                case Status.Spot.EndedNoOffer:
                case Status.Spot.Ended:
                case Status.Spot.Rejected:
                case Status.Spot.Deleted:
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

// public static class StatusUIChipsLook
// {
//     public static MudBlazor.Color Color {get; set;} = MudBlazor.Color.Dark;
// }