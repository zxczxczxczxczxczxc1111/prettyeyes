namespace PrettyEyes.Core.Platform;

/// <summary>What a registered combination does.</summary>
public enum HotkeyAction
{
    Region,
    FullScreen,
    Pin,
    HidePinned,
    ShowPinned,

    /// <summary>
    /// The laser pointer over the live screen. Unassigned out of the box like
    /// the three above it: it is a key held down during a demonstration, and
    /// which key that is depends entirely on what else is being demonstrated.
    /// </summary>
    Laser,
}

public interface IHotkeys : IDisposable
{
    /// <summary>
    /// Returns false when the combination is already taken by another process.
    /// Never throws for that case - it is expected, not exceptional.
    /// </summary>
    bool TryRegister(HotkeyAction action, HotkeyDefinition hotkey);

    void Unregister(HotkeyAction action);

    event EventHandler<HotkeyAction>? Pressed;

    /// <summary>
    /// Monitors were added, removed or rearranged. The frozen frame no longer
    /// matches the desktop, so whatever is on screen has to go.
    /// </summary>
    event EventHandler? DisplayChanged;
}
