namespace SpotTool.Web.Domain.Types;

//Zastanowic sie nad tym
//Ta sama klasa w DbModels
public class Trucks
{
    public string Name {get; set;} = string.Empty;
    public int MaxWeightInkg { get; set; }
    public int MaxWIncm { get; set; }
    public int MaxLIncm { get; set; }
    public int MaxHIncm { get; set; }
    public int MaxQm => MaxHIncm * MaxLIncm * MaxWIncm;
}