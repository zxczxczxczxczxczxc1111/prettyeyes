namespace PrettyEyes.Core.Platform;

public interface IHotkeys : IDisposable
{
    /// <summary>
    /// Returns false when the combination is already taken by another process.
    /// Never throws for that case - it is expected, not exceptional.
    /// </summary>
    bool TryRegister(HotkeyDefinition hotkey);

    void Unregister();

    event EventHandler? Pressed;
}
