using SpotTool.Web.Domain;

namespace SpotTool.Web.Db;

public static class DbModels
{    
    public record ContactPersonDetail(string Name, string Mobile, string Email);
    public class Spot
    {
        public Guid Id {get; set;} = Guid.CreateVersion7();
        public Guid UserId {get; set;}
        public string? Description {get; set;} // some kind of titly for a spot?
        public decimal TargetedCost {get; set;} = 0;
        public required ContactPersonDetail ContactDetails { get; set; } //in case we want to create spot from system user but assing special contact person
        public DateTimeOffset DeadLine { get; set; } = DateTimeOffset.UtcNow.AddMinutes(15).ToLocalTime();
        public Currency.Code CurrencyCode { get; set; } = Currency.Code.EUR;
        public Status.Spot CurrentStatus {get; set;} = Status.Spot.NotConfirmed;
        //public Guid? WinnerOfferId { get; set; } 
        public OfferSnapShot? WinnerOfferSnapShot { get; set; } //zastanowic sie nad tym 
        public DateTimeOffset CreatedAt {get; set;} = DateTimeOffset.UtcNow.ToLocalTime();
        public required UserSnapShot CreatedByUser { get; set; }
    }

    public class SpotStatusHistory
    {
        public Guid Id {get; set;} = Guid.CreateVersion7();
        public Guid SpotId { get; set; }
        public Status.Spot SpotStatus { get; set; } = Status.Spot.NotConfirmed;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public UserSnapShot? CreatedByUser { get; set; }
    }

    //Kazda oferta ma przypisane CreatedBy and ForUser w ten sposób 
    //Dispo dodaje oferte do spota dla Carrier - Negotiation wpwczas ValidTill = NegotiationDeadline a NegTargert to poprostu Value
    // a carrier dodaje oferte gdzie CreatedBy and For sa takie same
    //W ten sposób mamy info komu wyswietlac oferty (zawsze dla UserId = CreatedForUser.Id)
    //Nic nie modyfikujemy w tej encji tylko dodajemy nowe pola! Pełna historia ofert
    public record OfferSnapShot(Guid Id, UserSnapShot CreatedByUser, decimal Value, DateTimeOffset CreatedAt, Status.Offer Status, string Remark, DateTimeOffset ValidTill, Currency.Code CurrencyCode);
    public record UserSnapShot(Guid Id, Roles.User Role, Status.User Status, string Email);

    /*
    UPDATE public.mt_doc_dbmodels_offer
	SET data = jsonb_set(data, '{IsWinnerOffer}', 'true'::jsonb)
	WHERE id='01a0de05-78ea-72d9-aa57-05b911c23e73';
    */
    public class Offer
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public Guid SpotId { get; set; }
        public Guid UserId {get; set;} // FK for CreatedForUser 
        public Status.Offer OfferStatus { get; set; } = Status.Offer.Ok;
        public decimal Value { get; set; }
        public Currency.Code CurrencyCode { get; set; } = Currency.Code.EUR;
        public bool IsWinnerOffer { get; set; } = false;
        public string? Remarks {get; set;}
        public DateTimeOffset ValidTill { get; set; } = DateTimeOffset.UtcNow.AddHours(2).ToLocalTime();
        public DateTimeOffset CreatedAt {get; set;} = DateTimeOffset.UtcNow.ToLocalTime();
        public required UserSnapShot CreatedByUser { get; set; }
        public required UserSnapShot CreatedForUser { get; set; }
        //public OfferFeedbackSnapShot? OfferFeedbackSnapShot { get; set; } - to bedzie czesc kontaktu zwracanego do UI/API
    }

    // public record OfferFeedbackSnapShot (Guid OfferFeedbackSnapShotId, Status.OfferValue FeedbackStatus);
    // public class OfferFeedback
    // {
    //     public Guid Id { get; set; } = Guid.CreateVersion7();
    //     public Guid OfferId { get; set; }
    //     public Status.OfferValue FeedbackStatus { get; set; } = Status.OfferValue.Green;
        
    // }

    public class User
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public ContactPersonDetail? ContactDetails { get; set; }
        public Status.User UserStatus { get; set; } = Status.User.New;
        public Roles.User Role { get; set; } = Roles.User.Other;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToLocalTime();
        public DateTimeOffset? ModifiedAt { get; set; } = null;
    }

    public class UserCredential
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public Guid UserId { get; set; }
        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public required UserSnapShot User { get; set; }
    }
}
    