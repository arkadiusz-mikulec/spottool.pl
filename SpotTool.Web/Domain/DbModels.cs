using SpotTool.Web.Domain.Types;

namespace SpotTool.Web.Domain;

public static class DbModels
{    
    public record ContactPersonDetail(string Name, string Mobile, string Email);

    //https://www.google.com/search?q=korzystam+z+blibioteki+MArten+w+.net+10+i+tworze+baze+domument%C3%B3w.%0AZe+wzgledu+iz+dokumenty+nie+sa+polaczone+relacjami+oraz+encja+spot+nie+zawiera+listy+encji+oferta+mam+taka+sytuacja+jak+nizej.+jak+m%C3%B3g%C5%82bym+to+zoptymalizowac+jezeli+trzeba%0A%0AList%3CDbModels.Spot%3E+restOfSpots+%3D+%5B..+await+_session.Query%3CDbModels.Spot%3E%28%29%0A++++++++++++++++.Where%28m%3D%3Em.CurrentStatus+%21%3D+Status.Spot.Ok+%26%26%0A++++++++++++++++++++m.CurrentStatus+%21%3D+Status.Spot.Deleted+%26%26%0A++++++++++++++++++++m.CurrentStatus+%21%3D+Status.Spot.EndedNoOffers+%26%26%0A++++++++++++++++++++m.DeadLine+%3E%3D+start+%26%26+m.DeadLine+%3C%3D+stop%29%0A++++++++++++++++.ToAsyncEnumerable%28ct%29%0A++++++++++++++++.ToListAsync%28ct%29%0A++++++++++++%5D%3B%0A%0A++++++++++++foreach+%28var+spot+in+restOfSpots%29%0A++++++++++++%7B%0A++++++++++++++++var+offerExist+%3D+await+_session.Query%3CDbModels.Offer%3E%28%29%0A++++++++++++++++++++.AnyAsync%28m%3D%3Em.CreatedForUser.Id+%3D%3D+connectedUserId+%26%26+m.SpotId+%3D%3D+spot.Id%2C+ct%29%3B%0A%0A++++++++++++++++if%28offerExist%29%0A++++++++++++++++++++spots.Add%28spot%29%3B++++%0A++++++++++++%7D&sourceid=chrome&ie=UTF-8&amc=1&aep=42&cud=0&source=chrome.crn.rb&mstk=AUtExfDsqKdiWJa7QkyqaKm766xL5z679JCw4y1EvGt_0aX4fKwrQrdAPQtZcbEMA8MJSXg7NdQK1V8BDaG-1phayDlvkkuMO2yKEpVLrbV0uINTynuwhuShPuQzmQAjDnu8sJ8cZLKoISjdXrXVzClIQ2VXkR2UHgXNTTnPDBMPTiL4JsBQZEXmpxk5FpsTjQiVhe38ZQ9gIoe5sW7ijcpzpKnUUQvIwpsoaVEWrdWMpUXMjIljas8EpNzxHGzvLwUJumU_Mgnf4XNRyw&csuir=1&mtid=No_HavTZIP6F7NYPyYu02Qk&udm=50
    public class Spot
    {
        public Guid Id {get; set;} = Guid.CreateVersion7();
        public Guid UserId {get; set;}
        public string? Description {get; set;} // some kind of titly for a spot?
        public decimal TargetedCost {get; set;} = 0;
        public ContactPersonDetail? ContactDetails { get; set; } //in case we want to create spot from system user but assing special contact person
        public DateTimeOffset DeadLine { get; set; } = DateTimeOffset.Now.AddMinutes(15);
        public Currency.Code CurrencyCode { get; set; } = Currency.Code.EUR;
        public Status.Spot CurrentStatus {get; set;} = Status.Spot.NotConfirmed;
        //public Guid? WinnerOfferId { get; set; } 
        public OfferSnapShot? WinnerOfferSnapShot { get; set; } //zastanowic sie nad tym 
        public DateTimeOffset CreatedAt {get; set;} = DateTimeOffset.Now;
        public required UserSnapShot CreatedByUser { get; set; }
        public List<Guid>? BiddersId {get;set; } = null; // UserId which placed offer
    }

    public class SpotStatusHistory
    {
        public Guid Id {get; set;} = Guid.CreateVersion7();
        public Guid SpotId { get; set; }
        public Status.Spot SpotStatus { get; set; } = Status.Spot.NotConfirmed;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
        public UserSnapShot? CreatedByUser { get; set; }
    }

    //Kazda oferta ma przypisane CreatedBy and ForUser w ten sposób 
    //Dispo dodaje oferte do spota dla Carrier - Negotiation wpwczas ValidTill = NegotiationDeadline a NegTargert to poprostu Value
    // a carrier dodaje oferte gdzie CreatedBy and For sa takie same
    //W ten sposób mamy info komu wyswietlac oferty (zawsze dla UserId = CreatedForUser.Id)
    //Nic nie modyfikujemy w tej encji tylko dodajemy nowe pola! Pełna historia ofert
    public record OfferSnapShot(Guid Id, UserSnapShot CreatedByUser, decimal Value, DateTimeOffset CreatedAt, Status.Offer Status, string Remark, 
        DateTimeOffset ValidTill, Currency.Code CurrencyCode);
    public record UserSnapShot(Guid Id, List<Role.User> Roles, Status.User Status, ContactPersonDetail PersonDetail);

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
        public Status.Offer Status { get; set; } = Types.Status.Offer.Ok;
        public Status.OfferValue ValueStatus { get; set; } = Types.Status.OfferValue.Green;
        public decimal Value { get; set; }
        public Currency.Code CurrencyCode { get; set; } = Currency.Code.EUR;
        public bool IsWinnerOffer { get; set; } = false;
        public string? Remarks {get; set;}
        public DateTimeOffset ValidTill { get; set; } = DateTimeOffset.Now.AddHours(2);
        public DateTimeOffset CreatedAt {get; set;} = DateTimeOffset.Now;
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
        public required ContactPersonDetail ContactDetails { get; set; }
        public Status.User Status { get; set; } = Domain.Types.Status.User.New;
        public List<Role.User> Roles { get; set; } = [Role.User.Bidder];
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
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
    