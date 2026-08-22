using PrettyEyes.Platform.Windows.Native;

namespace PrettyEyes.Platform.Windows;

/// <summary>
/// Hands the working set back to Windows after a long idle spell.
///
/// Read this before believing the number it produces. Task Manager's "Memory"
/// column is the private working set: the pages this process has resident right
/// now. Trimming does not free anything - the memory stays committed, the pages
/// go to the standby list, and the first touch after that pays a soft fault to
/// get them back. Nothing about the application gets lighter.
///
/// What does change is the only number anybody ever quotes at us, and the whole
/// reason this exists is that a screenshot tool sitting at 210 MB in Task
/// Manager loses the argument against Lightshot before it is ever opened.
///
/// Called from one place only: the idle timer, together with letting go of the
/// capture engine and purging the Skia caches. Doing it while somebody is
/// working would trade their next frame for a prettier number.
/// </summary>
public static class WorkingSet
{
    /// <summary>False when Windows refused; there is nothing to do about it.</summary>
    public static bool Trim() => NativeMethods.SetProcessWorkingSetSize(
        NativeMethods.GetCurrentProcess(),
        minimum: -1,
        maximum: -1);
}
