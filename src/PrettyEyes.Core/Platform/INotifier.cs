namespace PrettyEyes.Core.Platform;

public interface INotifier
{
    /// <summary>
    /// Shows a balloon from the tray icon. Used when there is no window on
    /// screen to put a message into.
    /// </summary>
    void Notify(string title, string message);
}
