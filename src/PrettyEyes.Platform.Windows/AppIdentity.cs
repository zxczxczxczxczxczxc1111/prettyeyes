using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// The identity Windows groups our windows and notifications under. It has to
/// match the AppUserModelID the installer puts on the Start Menu shortcut, and
/// it differs between the release and the check build so that Windows does not
/// merge two applications into one.
/// </summary>
public static class AppIdentity
{
    public static void Declare(string appUserModelId) =>
        NativeMethods.SetCurrentProcessExplicitAppUserModelID(appUserModelId);
}
