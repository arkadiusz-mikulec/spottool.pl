using ImTools;
using JasperFx.CodeGeneration.Frames;

namespace SpotTool.Web.Domain.Types;

public static class Status
{
    public enum Spot
    {
        NotConfirmed,
        Ok,
        Deleted,
        Bidding,
        Canceled,
        EndedNoOffers,
        EndedCostOK,
        EndedCostTooHigh,
        Negotiation,
        Accepted,
        Rejected
    }

    
    public static string GetStatusName(Status.Spot spotStatus, Role.User userRole)
    {
        if(userRole == Role.User.Issuer)
            return spotStatus.ToString();
        
        string toConfirm = "TO CONFIRM";
        
        switch(spotStatus)
        {
            case Spot.Ok: return Spot.Bidding.ToString().ToUpper();
            case Spot.EndedCostOK:
            case Spot.EndedCostTooHigh: return toConfirm;
            default: return spotStatus.ToString().ToUpper();
        }
    }


    public enum OfferValue
    {
        Green,
        Yellow,
        Red
    }

    public enum Offer
    {
        Ok,
        Rejected,
        Deleted,
        ToNegotiation,
        Negotiated
    }

    public enum User
    {
        New,
        NotConfirmed,
        Ok,
        Blocked,
        Deleted
    }
}
