using System.Reflection;

namespace PrettyEyes.Core.Platform;

/// <summary>
/// Which build this is, and every name that has to differ because of it.
///
/// One place on purpose: the check build lives next to the real one, and a
/// single shared name is enough for it to take over the real one's settings,
/// its autostart entry or its place in the installed programs list.
/// </summary>
public sealed record AppFlavor(
    string DisplayName,
    string DataFolder,
    string MutexName,
    string AppUserModelId,
    string AutostartValueName,
    string IconAsset,
    bool UpdatesAllowed)
{
    /// <summary>What the build script writes into the assembly.</summary>
    private const string Marker = "check";

    private const string MetadataKey = "BuildFlavor";

    public static AppFlavor Release { get; } = new(
        DisplayName: "prettyeyes",
        DataFolder: "prettyeyes",
        MutexName: "PrettyEyesSingleInstance",
        AppUserModelId: "prettyeyes.app",
        AutostartValueName: "prettyeyes",
        IconAsset: "avares://PrettyEyes.App/Assets/prettyeyes.ico",
        UpdatesAllowed: true);

    public static AppFlavor Check { get; } = new(
        DisplayName: "prettyeyes проверка",
        DataFolder: "prettyeyes-check",
        MutexName: "PrettyEyesCheckSingleInstance",
        AppUserModelId: "prettyeyes.check",
        AutostartValueName: "prettyeyes-check",
        IconAsset: "avares://PrettyEyes.App/Assets/prettyeyes-check.ico",
        UpdatesAllowed: false);

    /// <summary>What this running build is, decided once at start-up.</summary>
    public static AppFlavor Current { get; } = From(FromAssembly());

    /// <summary>
    /// Anything other than the marker is the release build: a flag that failed
    /// to arrive must not produce a half-check build with a mixed identity.
    /// </summary>
    public static AppFlavor From(string? marker) =>
        string.Equals(marker?.Trim(), Marker, StringComparison.OrdinalIgnoreCase)
            ? Check
            : Release;

    private static string? FromAssembly() => Assembly.GetEntryAssembly()
        ?.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == MetadataKey)
        ?.Value;
}
