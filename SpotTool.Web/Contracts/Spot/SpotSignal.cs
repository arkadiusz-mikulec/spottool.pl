using SpotTool.Web.Contracts.Spot;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;

namespace SpotTool.Web.Contracts;

public enum SignalType
    {
        SpotCreate, //done
        SpotStatusUpdate, //done
        SpotDeadLineUpdate, //done
        SpotCancel, //Done by SpotStatusUpdate
        SpotDelete, //Done by SpotStatusUpdate
        SpotEdit,
        SpotApprove, //done
        OfferCreate, //done
        OfferReject //done
    }
    // Generyczny kontener przesyłający akcję oraz powiązany z nią obiekt danych
public record SpotSignal(
    SignalType Type, 
    SpotUIRespond? SpotUiRespond = null, 
    SpotStatusUIRespond? SpotStatusUIRespond = null, 
    DbModels.Offer? Offer = null, //nie uzywm poki co. Przy odaniu oferty aktualizuje SpotStatusUIRespond!
    SpotDeadlineUpdateRespond? SpotDeadlineUpdateRespond = null
);
    