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
        EndedNoOffer,
        Ended,
        Negotiation,
        Accepted,
        Rejected
    }

    
    public static Status.Spot GetStatusName(Status.Spot spotStatus, Role.User userRole)
    {
        if(userRole == Role.User.Issuer)
            return spotStatus;


        return spotStatus switch
        {
            Spot.Ok => Spot.Bidding,
            _ => spotStatus,
        };
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
