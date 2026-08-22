using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Whether a game has the screen to itself in the exclusive sense.
///
/// This started as "does the window in front cover a whole monitor", and that
/// was wrong: borderless fullscreen is a plain window the size of the screen,
/// and there is nothing wrong with a pin sitting on top of one. osu! in
/// borderless was answered by a pin that vanished behind it and came back a
/// second later, over and over, which is worse than the problem being solved.
///
/// The problem being solved is narrower than it looked. A topmost window costs
/// something only in exclusive Direct3D fullscreen, where Windows has to take
/// the game out of its exclusive mode to draw anything above it; the game asks
/// the mode back, and the screen flickers between them until somebody gives up.
///
/// Windows answers exactly that question, and it is the same answer the shell
/// uses to decide whether a notification may interrupt: this is why toasts
/// stay quiet during a game and not during a maximised browser.
/// </summary>
public static class FullScreenApp
{
    /// <summary>A Direct3D application is running in exclusive full screen.</summary>
    private const int RunningD3DFullScreen = 3;

    /// <summary>
    /// True only for the case where being on top would start a fight. Anything
    /// else - borderless, maximised, a video player, the desktop - is a window
    /// like any other, and a pin belongs above it.
    /// </summary>
    public static bool TakesTheScreen()
    {
        // A failure here means we do not know, and not knowing has to read as
        // "carry on": the pin staying on top is what it is for.
        if (NativeMethods.SHQueryUserNotificationState(out var state) != 0)
        {
            return false;
        }

        return state == RunningD3DFullScreen;
    }
}
