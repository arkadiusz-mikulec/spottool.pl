using SpotTool.Web.Contracts.Spot;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;

namespace SpotTool.Web.Contracts;

public enum SignalType
    {
        SpotAdded,
        SpotStatusUpdate
    }
    // Generyczny kontener przesyłający akcję oraz powiązany z nią obiekt danych
public record SpotSignal(SignalType Type, SpotUIRespond? Spot, SpotStatusUIRespond? StatusUpdate);
    