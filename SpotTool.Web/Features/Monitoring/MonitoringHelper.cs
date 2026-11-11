using SpotTool.Web.Domain.Helpers;

namespace SpotTool.Web.Features.Monitoring;

public interface IMonitoringHelper
{
    List<MonitoringItem> GetMonitoring(bool issuer, string user, int? id = null);
    //void SetMonitoringItem(MonitoringItem item);
    bool RemoveItem(MonitoringItem item);    
    void AddItems(string issuer, string bidder, string _to, int count = 1);
    //Dictionary<Guid, string> GetUsers();
    List<HistoryItem> GetHistory(int monitoringItemId);
    void AddHistoryItem(int monitoringItemId, HistoryItem item);
    void AddHistory(int monitoringItemId, List<HistoryItem> history);
}

public class MonitoringHelper(IDateTimeProvider _dateTimeProvider): IMonitoringHelper
{
    private List<MonitoringItem> _monitoring {get; set;} = [];

    public List<MonitoringItem> GetMonitoring(bool issuer, string user, int? id = null)
    {
        if(id is null)
        {
            return issuer ? [.. _monitoring.Where(m=>m.dispo.Equals(user, StringComparison.CurrentCultureIgnoreCase))] :
                [.. _monitoring.Where(m=>m.carrier.Equals(user, StringComparison.CurrentCultureIgnoreCase) && m.unloaded == false)];
        }        
        else
            return [.. _monitoring.Where(m=>m.id == id && m.unloaded == false)];
    }

    public bool RemoveItem(MonitoringItem item)
    {
        try 
        {
            _monitoring.Remove(item);
            return true;
        }
        catch
        {
            return false;
        }
    }
    public void AddItems(string issuer, string bidder, string _to, int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            int pallets = Random.Shared.Next(2, 100);
            float randomKg = (float)Random.Shared.Next(50, 2500);
            
            HistoryItem historyItem = new()
            {
                //id = 1,
                insertDate = _dateTimeProvider.Now(),
                _event = "CREATE",
                info = "Position was created"
            };

            if(pallets % 2 == 0)
                _monitoring.Add(new MonitoringItem() {id = i, transportOrder = _to, companies = ["comp01", "comp02", "comp03"], loadingTill = DateTime.Now.AddMinutes(pallets), 
                    dispo = issuer, carrier = bidder,
                    packages = pallets, packagesLoaded = pallets,
                    kg = randomKg, kgLoaded = randomKg, history = [historyItem] });
            else
                _monitoring.Add(new MonitoringItem() {id = i, transportOrder = _to, companies = [bidder], company = bidder, loadingTill = DateTime.Now.AddMinutes(pallets), 
                    dispo = issuer, carrier = bidder,
                    packages = pallets, packagesLoaded = pallets,
                    kg = randomKg, kgLoaded = randomKg, history = [historyItem] });

            // if(i % 5 == 0)
            //     _monitoring.Add(new MonitoringItem() {id = i, transportOrder = $"324567-{i}", companies = ["comp01", "comp02", "cpmp03"], loadingTill = DateTime.Now.AddMinutes(pallets), 
            //         dispo = issuer, carrier = bidder,
            //         packages = pallets, packagesLoaded = pallets,
            //         kg = randomKg, kgLoaded = randomKg, history = [historyItem] });   
            // else
            // {
            //     var index = Random.Shared.Next(1, carriers.Length - 1);
            //     _monitoring.Add(
            //         new MonitoringItem() {id = i, transportOrder = $"324567-{i}", company = $"company-{carriers[index]}", companies = [$"company-{carriers[index]}"], loadingTill = DateTime.Now.AddMinutes(pallets), 
            //             dispo = dispo, carrier = carriers[index],
            //             packages = pallets, packagesLoaded = pallets,
            //             kg = randomKg, kgLoaded = randomKg, history = [historyItem] });
            // }
        }
    }
    // public Dictionary<Guid, string> GetUsers()
    // {
    //     return _users;
    // }

    public List<HistoryItem> GetHistory(int monitoringItemId)
    {
        return _monitoring[monitoringItemId].history;
    }

    public void AddHistoryItem(int monitoringItemId, HistoryItem item)
    {
        _monitoring[monitoringItemId].history.Add(item);
    }

    public void AddHistory(int monitoringItemId, List<HistoryItem> history)
    {
        _monitoring[monitoringItemId].history.AddRange(history);
    }
    
}

public class MonitoringItem
{ 
    public int id {get; set;}
    public string dispo {get; set;} = string.Empty;
    public string carrier {get; set;} = string.Empty;
    public string client {get; set;} = "FCA";
    public string transportOrder {get; set;} = string.Empty;
    public int packages {get; set;}
    public int packagesLoaded {get; set;}
    public float kg {get; set;} 
    public float kgLoaded {get; set;}
    public bool? approved {get; set;}
    public bool loaded {get; set;} = false;
    public bool unloaded {get; set;} = false;
    public string company {get; set;} = string.Empty;
    public string truckNo {get; set;} = string.Empty;
    public List<string> companies {get; set;} = [];
    public DateTimeOffset loadingTill {get; set;}
    public DateTimeOffset? modifiedDateTime {get; set;} = null;
    public DateTimeOffset? loadedDateTime {get; set;} = null;
    public DateTimeOffset? unloadedDateTime {get; set;} = null;
    public string reason {get; set;} = "OK";
    public List<HistoryItem> history {get; set;} = [];

    public override string ToString()
    {
        return $"Id: {id}, TO: {transportOrder}";
    }
}
public class HistoryItem
{
    //public int id { get; set; }
    public string _event { get; set; } = string.Empty;
    public DateTimeOffset insertDate {get; set;}
    public string info { get; set; } = string.Empty;
}