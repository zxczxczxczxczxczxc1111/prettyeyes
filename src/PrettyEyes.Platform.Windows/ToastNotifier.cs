using System.Runtime.InteropServices;
using PrettyEyes.Core.Platform;
using global::Windows.Data.Xml.Dom;
using global::Windows.UI.Notifications;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Toast notifications. Windows shows these only for an application it can
/// identify, which means a Start Menu shortcut carrying our AppUserModelID -
/// the installer creates one. A portable copy has no such shortcut, so the
/// call fails and the tray balloon takes over.
/// </summary>
public sealed class ToastNotifier : INotifier
{
    private readonly string _appUserModelId;
    private readonly INotifier _fallback;

    public ToastNotifier(string appUserModelId, INotifier fallback)
    {
        _appUserModelId = appUserModelId;
        _fallback = fallback;
    }

    public void Notify(string title, string message)
    {
        try
        {
            var xml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            var texts = xml.GetElementsByTagName("text");
            texts[0].AppendChild(xml.CreateTextNode(title));
            texts[1].AppendChild(xml.CreateTextNode(message));

            ToastNotificationManager.CreateToastNotifier(_appUserModelId).Show(new ToastNotification(xml));
        }
        catch (COMException)
        {
            _fallback.Notify(title, message);
        }
        catch (ArgumentException)
        {
            // Thrown when the id is not registered with the shell at all.
            _fallback.Notify(title, message);
        }
    }
}
