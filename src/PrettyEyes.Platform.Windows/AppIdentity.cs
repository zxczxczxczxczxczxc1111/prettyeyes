using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// The identity Windows groups our windows and notifications under. It has to
/// match the AppUserModelID the installer puts on the Start Menu shortcut.
/// </summary>
public static class AppIdentity
{
    public const string AppUserModelId = "prettyeyes.app";

    public static void Declare() => NativeMethods.SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
}
