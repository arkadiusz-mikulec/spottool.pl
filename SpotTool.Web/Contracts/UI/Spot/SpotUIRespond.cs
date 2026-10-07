using SpotTool.Web.Domain;

namespace SpotTool.Web.Contracts.UI.Spot;

public class SpotUIRespond : DbModels.Spot
{
    public string StatusDescription { get; set; } = string.Empty;
    public SpotButtons SpotButtons {get; set;} = new();
}

public class SpotButtons
{
    public Button ConfirmButton { get; set; } = new Button {ButtonText = "Confirm", ButtonIcon = "edit"}; 
    public Button EditButton { get; set; } = new Button {ButtonText = "Edit", ButtonIcon = "edit"}; 
    public Button AddOfferButton {get; set;} =  new Button {ButtonText = "Add offer", ButtonIcon = "add"};  
}

public class Button
{
    public string ButtonText { get; set; } = string.Empty;
    public string ButtonIcon { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
}