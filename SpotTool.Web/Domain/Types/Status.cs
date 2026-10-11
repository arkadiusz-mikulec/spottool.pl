using ImTools;

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

    public static class SpotName
    {
        public static string Get(Status.Spot spotStatus, Role.User userRole)
        {
            if(userRole == Role.User.Issuer)
                return spotStatus.ToString();
            
            switch(spotStatus)
            {
                case Spot.Ok: return Spot.Bidding.ToString().ToUpper();
                case Spot.EndedCostOK:
                case Spot.EndedCostTooHigh: return "TO CONFIRM";
                default: return spotStatus.ToString().ToUpper();
            }
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
