using SpotTool.Web.Contracts.Spot;
using SpotTool.Web.Domain;

namespace SpotTool.Web.Contracts;

public enum SignalType
    {
        SpotAdded,
        SpotStatusUpdate
    }
    // Generyczny kontener przesyłający akcję oraz powiązany z nią obiekt danych
public record SpotSignal(SignalType Type, DbModels.Spot? Spot, SpotStatusUpdateRequest? StatusUpdate);
    