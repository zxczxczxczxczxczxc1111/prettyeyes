using Microsoft.Win32;
using PrettyEyes.Core.Platform;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Autostart through HKCU\...\Run. Per-user on purpose: the machine-wide key
/// needs administrator rights, and a screenshot tool has no business asking.
/// </summary>
public sealed class RegistryAutostart : IAutostart
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "prettyeyes";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(ValueName) is string value && value.Length > 0;
        }
    }

    /// <summary>
    /// Rewrites a dead entry to point at this executable.
    ///
    /// Found on a real machine: autostart was switched on months ago by a build
    /// that no longer exists, and the entry still pointed at its folder. The
    /// checkbox said "on", Windows started nothing, and there was no way for
    /// anyone to notice. Only a path that is gone gets rewritten, so a copy
    /// started by hand never quietly steals autostart from the installed one.
    /// </summary>
    public bool Heal()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);

            if (key?.GetValue(ValueName) is not string stored || stored.Length == 0)
            {
                return false;
            }

            var pointsAt = stored.Trim('"');

            if (File.Exists(pointsAt))
            {
                return false;
            }

            var path = Environment.ProcessPath;

            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            key.SetValue(ValueName, $"\"{path}\"");

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath);

            if (key is null)
            {
                return false;
            }

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            var path = Environment.ProcessPath;

            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            // Quoted: an unquoted path with spaces silently fails to start.
            key.SetValue(ValueName, $"\"{path}\"");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
