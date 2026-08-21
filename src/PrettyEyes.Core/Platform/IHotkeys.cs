namespace PrettyEyes.Core.Platform;

/// <summary>What a registered combination does.</summary>
public enum HotkeyAction
{
    Region,
    FullScreen,
    Pin,
    HidePinned,
    ShowPinned,
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
