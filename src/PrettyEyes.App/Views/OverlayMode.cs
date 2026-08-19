namespace PrettyEyes.App.Views;

/// <summary>
/// What the pointer does in the overlay. The overlay starts by picking a
/// region, then lets the user fix it, and only draws once a tool is picked.
/// </summary>
public enum OverlayMode
{
    Selecting,
    Adjusting,
    Drawing,
}
