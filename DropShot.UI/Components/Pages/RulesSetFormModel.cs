namespace DropShot.UI.Components.Pages;

/// <summary>
/// Form-state for the add/edit RulesSet dialog. Lives next to the page so the
/// dialog and the page can both bind to the same shape without crossing the
/// service boundary.
/// </summary>
public class RulesSetFormModel
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}
