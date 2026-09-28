using System.Collections.Concurrent;
using SpotTool.Web.Domain;

namespace SpotTool.Web.Infrastructure;

public class SeedDataInMemory
{
    public ConcurrentDictionary<Guid, DbModels.Spot> Spots {get; private set;}
    // public ConcurrentDictionary<Guid, string> Offer {get; private set;}
    // public ConcurrentDictionary<Guid, string> Spots {get; private set;}
    public SeedDataInMemory()
    {
        Spots = new ConcurrentDictionary<Guid, DbModels.Spot>();

        //if(!Spots.TryAdd(InitModels.spotId, InitModels.Spots()[0]))
        Spots[InitModels.spotId] = InitModels.Spots()[0];
    }

    
}