namespace SpotTool.Web.Domain;

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
        ManuallyAccepted,
        ManuallyRejected,
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
        Negotiation,
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

public static class Roles
{
    public enum User
    {
        Admin,
        Manager,
        Disponent,
        Carrier,
        Other
    }
}

public static class Currency
{
    public enum Code
    {
        EUR,
        USD
        
    }
}