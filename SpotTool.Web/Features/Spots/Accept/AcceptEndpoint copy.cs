// using System.Threading.Channels;
// using FastEndpoints;
// using Marten;
// using SpotTool.Web.Contracts.UI.Spot;
// using SpotTool.Web.Domain;
// using SpotTool.Web.Domain.Helpers;
// using SpotTool.Web.Domain.Types;
// using SpotTool.Web.Features.Shared.Channels;
// using SpotTool.Web.Features.Shared.Spots;

// namespace SpotTool.Web.Features.Spots.Accept;

// public record Request(Guid OfferId);
// public record Response(bool Accepted);

// public class AcceptEndpoint(
//     IDocumentSession _session, 
//     SpotDistribution _spotDistribution,
//     SpotService _spotService) : Endpoint<Request, Response>
// {
//     //private readonly IDocumentStore _store = store; // Marten wstrzyknięty klasycznie przez DI
    
//     public override void Configure()
//     {
//         Patch("/api/v1/integrations/spots/{Id}/accept");
//         AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
//     }

//     public override async Task HandleAsync(Request req, CancellationToken ct)
//     {
//         // Logika biznesowa zapisu do Martena/Postgresa
//         var id = Route<Guid>("Id");
//         //Console.WriteLine($"ID: {id}");
//         var spot = await _session.LoadAsync<DbModels.Spot>(id, ct);
//         if(spot is null)
//         {
//             await Send.NotFoundAsync(ct);
//             return; //Task.FromResult(new ProblemDetails();
//         }
        
//         if(!CanBeAccepted(spot))
//         {
//             await Send.ResultAsync(TypedResults.Problem(
//                 detail: $"Spot cannot be accepted because is in a wrong status: {spot.CurrentStatus}",
//                 statusCode: StatusCodes.Status400BadRequest,
//                 title: "Spot cannot be accepted"
//             ));
            
//             return;
//         }

//         var winnerOffer = await _session.Query<DbModels.Offer>()
//             .Where(m=>m.SpotId == spot.Id && m.Id == req.OfferId)
//             .FirstOrDefaultAsync(ct);

//         if(winnerOffer is null)
//         {
//             await Send.NotFoundAsync(ct);
//             return; //Task.FromResult(new ProblemDetails();
//         }

//         if(!CanOfferBeSelected(winnerOffer))
//         {
//             await Send.ResultAsync(TypedResults.Problem(
//                 detail: "Offer cannot be selected as winner one because is in a wrong status",
//                 statusCode: StatusCodes.Status400BadRequest,
//                 title: "Offer cannot be selected"
//             ));
            
//             return;
//         }

//         winnerOffer.IsWinnerOffer = true;
//         _session.Store(winnerOffer);

//         spot.CurrentStatus = Status.Spot.Accepted;
//         spot.WinnerOfferSnapShot = new DbModels.OfferSnapShot(
//             winnerOffer.Id,
//             winnerOffer.CreatedByUser,
//             winnerOffer.Value,
//             winnerOffer.CreatedAt,
//             winnerOffer.Status,
//             winnerOffer.Remarks,
//             winnerOffer.ValidTill,
//             winnerOffer.CurrencyCode);    

//         _session.Store(spot);
        
//         _session.Store(new DbModels.SpotStatusHistory
//         {
//             SpotId = spot.Id,
//             SpotStatus = spot.CurrentStatus
//         });                    

//         await _session.SaveChangesAsync(ct);

//         var spotModel = new SpotModel(
//             spot, 
//             [winnerOffer], 
//             await _spotService.GetSpotGroups(spot.Id, ct),
//             await _spotService.GetUsersExcluded(spot.Id, ct)
//         );
//         _ = _spotDistribution.SendSpotStatusToAuthorizedUsers(spotModel);
//         //_wakeupChannel.Writer.TryWrite(spot.Id);
//         // // Błyskawiczna odpowiedź 201 Created
//         // //await Send.CreatedAtAsync<GetById.GetByIdEndpoint>(new { Id = id }, new Response(id), cancellation: ct);
//         await Send.OkAsync(new Response(true), ct);
//     }

//     private static bool CanBeAccepted(DbModels.Spot spot)
//     {
//         return spot.CurrentStatus switch
//         {
//             Status.Spot.Ended or Status.Spot.Rejected => true,
//             _ => false
//         };
//     }

//     private static bool CanOfferBeSelected(DbModels.Offer offer)
//     {
//         return offer.Status switch
//         {
//             Status.Offer.Deleted or Status.Offer.ToNegotiation => false,
//             _ => true
//         };
//     }
    
// }