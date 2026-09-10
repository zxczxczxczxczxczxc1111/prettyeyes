using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// A finer system timer, for as long as something is actually animating.
///
/// The default tick is about 15.6 ms, so a sixteen-millisecond timer waits
/// through two of them roughly a third of the time and averages forty-four
/// frames a second. Measured, not assumed: the laser pointer reported exactly
/// 44 before this and asks for 60.
///
/// Held rather than set once. A finer tick costs power across the whole
/// machine, so it is asked for while the pointer is on screen and given back
/// the moment it is not - and it is counted, because two things asking for it
/// and one giving it back would take it away from the other.
/// </summary>
public static class FineTimers
{
    private const uint OneMillisecond = 1;

    private static readonly Lock Gate = new();

    private static int _holders;

    public static void Hold()
    {
        lock (Gate)
        {
            if (_holders++ == 0)
            {
                NativeMethods.timeBeginPeriod(OneMillisecond);
            }
        }
    }

    public static void Release()
    {
        lock (Gate)
        {
            if (_holders == 0)
            {
                return;
            }

            if (--_holders == 0)
            {
                NativeMethods.timeEndPeriod(OneMillisecond);
            }
        }
    }
}
