using SpotTool.Web.Domain.Types;

namespace SpotTool.Web.Contracts.UI.Offer;

public record LastBidderOfferRespond(decimal Offer, Status.OfferValue OfferValueStatus);