namespace SpotTool.Web.Features.Shared;

public static class DbModels
{    
    public enum SpotStatu
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

    public enum OfferStatus
    {
        Green,
        Yellow,
        Red,
        Rejected
    }

    public class Spot
    {
        public Guid Id {get; set;} = Guid.CreateVersion7();
        public Guid UserId {get; set;}
        public string? Description {get; set;}
        public decimal TargetedCost {get; set;} = 0;
        public DateTime DeadLine { get; set; } = DateTime.UtcNow.AddMinutes(15).ToLocalTime();
        public SpotStatu Status {get; set;} = SpotStatu.NotConfirmed;
        public DateTime CreatedAt {get; set;} = DateTime.UtcNow.ToLocalTime();
    }

    public class Offer
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public Guid SpotId { get; set; }
        public Guid UserId {get; set;}
        public OfferStatus Status { get; set; } = OfferStatus.Green;
        public decimal Value { get; set; }
        public DateTime CreatedAt {get; set;} = DateTime.UtcNow.ToLocalTime();
    }

    public class User
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public string Email {get; set;} = string.Empty;
    }
}
    