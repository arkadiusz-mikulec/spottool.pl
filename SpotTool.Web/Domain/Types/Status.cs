namespace SpotTool.Web.Domain.Types;

public static class Status
{
    public enum Spot
    {
        NotConfirmed,
        Ok,
        Deleted,
        DuringBidding,
        Canceled,
        EndedNoOffers,
        EndedCostOK,
        EndedCostTooHigh,
        DuringNegotiation,
        Accepted,
        Rejected
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
