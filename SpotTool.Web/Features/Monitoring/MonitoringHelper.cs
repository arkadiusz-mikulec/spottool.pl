namespace SpotTool.Web.Features.Monitoring;

public interface IMonitoringHelper
{
    List<MonitoringItem> GetMonitoring(string? user, int? id);
    //void SetMonitoringItem(MonitoringItem item);
    bool RemoveItem(MonitoringItem item);    
    void AddItems(int count);
    Dictionary<Guid, string> GetUsers();
    List<HistoryItem> GetHistory(int monitoringItemId);
    void AddHistoryItem(int monitoringItemId, HistoryItem item);
    void AddHistory(int monitoringItemId, List<HistoryItem> history);
}

public class MonitoringHelper: IMonitoringHelper
{
    private List<MonitoringItem> _monitoring {get; set;} = [];
    private Dictionary<Guid, string> _users {get; set;} = [];

    private string dispo = "arkadiusz.mikulec@gmail.com";
    private string[] carriers = ["arek@e-site.pl", "carrier11@op2.pl", "carrier12@op2.pl", "carrier21@op2.pl", "carrier22@op2.pl"];
    private string[] _events = ["CREATE", "LOADED", "GPS", "GPS", "UNLOADED"];
    private string[] _infos = ["Position added to monitoring", "Truck loaded", "GPS send requeste", "GPS respond", "Truck unloaded"];
    public MonitoringHelper()
    {
        foreach (var item in carriers)
        {
            _users.Add(Guid.CreateVersion7(), item);
        }
        AddItems(6);

        List<HistoryItem> history = [];
        for (int i = 0; i < _events.Length; i++)
        {
            history.Add(new HistoryItem
            {
                id = i,
                _event = _events[i],
                insertDate = DateTime.Now,
                info = _infos[i]
            });
        }

        foreach (var item in _monitoring)
        {
            this.AddHistory(item.id, history);    
        }
    }

    public List<MonitoringItem> GetMonitoring(string? user = null, int? id = null)
    {
        if(user is null && id is null)
            return _monitoring;
        else if(id is null)
            return [.. _monitoring.Where(m=>m.carrier.Equals(user) && m.unloaded == false)];
        else
            return [.. _monitoring.Where(m=>m.id == id && m.unloaded == false)];
    }

    // public void SetMonitoringItem(MonitoringItem item)
    // {
    //     _monitoring[item.id] = item;
    // }
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
    public void AddItems(int count)
    {
        for (int i = 0; i < count; i++)
        {
            int pallets = Random.Shared.Next(2, 100);
            float randomKg = (float)Random.Shared.Next(50, 2500);
            
            HistoryItem historyItem = new()
            {
                id = 1,
                insertDate = DateTime.Now,
                _event = _events[0],
                info = _infos[0]
            };
                       
            if(i % 5 == 0)
                _monitoring.Add(new MonitoringItem() {id = i, transportOrder = $"324567-{i}", companies = ["comp01", "comp02", "cpmp03"], loadingTill = DateTime.Now.AddMinutes(pallets), 
                    dispo = dispo, carrier = carriers[0],
                    packages = pallets, packagesLoaded = pallets,
                    kg = randomKg, kgLoaded = randomKg, history = [historyItem] });   
            else
            {
                var index = Random.Shared.Next(1, carriers.Length - 1);
                _monitoring.Add(
                    new MonitoringItem() {id = i, transportOrder = $"324567-{i}", company = $"company-{carriers[index]}", companies = [$"company-{carriers[index]}"], loadingTill = DateTime.Now.AddMinutes(pallets), 
                        dispo = dispo, carrier = carriers[index],
                        packages = pallets, packagesLoaded = pallets,
                        kg = randomKg, kgLoaded = randomKg, history = [historyItem] });
            }
        }
    }
    public Dictionary<Guid, string> GetUsers()
    {
        return _users;
    }

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
    public DateTime loadingTill {get; set;}
    public DateTime? modifiedDateTime {get; set;}
    public DateTime? loadedDateTime {get; set;}
    public DateTime? unloadedDateTime {get; set;}
    public string reason {get; set;} = "OK";
    public List<HistoryItem> history {get; set;} = [];

    public override string ToString()
    {
        return $"Id: {id}, TO: {transportOrder}";
    }
}
public class HistoryItem
{
    public int id { get; set; }
    public string _event { get; set; } = string.Empty;
    public DateTime insertDate {get; set;}
    public string info { get; set; } = string.Empty;
}