using PrettyEyes.Core.Rendering;

namespace PrettyEyes.Core.Settings;

/// <summary>
/// Where finished screenshots go without asking.
///
/// When this is on, both buttons write a file: copy puts the shot in the
/// clipboard and on disk, save writes it silently. The point is that a
/// screenshot lands in a folder and nothing interrupts.
/// </summary>
public sealed record SaveOptions(bool Enabled, string Folder, string Template)
{
    public static SaveOptions Default => new(false, string.Empty, FileNameTemplate.Default);

    /// <summary>Nothing to write into is the same as being switched off.</summary>
    public bool Ready => Enabled && !string.IsNullOrWhiteSpace(Folder);
}
