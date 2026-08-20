namespace PrettyEyes.Core.Updates;

/// <summary>Where releases come from. One implementation, one place to distrust.</summary>
public interface IUpdateSource
{
    /// <summary>The newest release, or null when there is none or the network said no.</summary>
    Task<ReleaseInfo?> LatestAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Downloads the installer and returns the path, or null if anything at all
    /// went wrong: a wrong hash, a wrong size, a broken connection.
    /// </summary>
    Task<string?> DownloadAsync(ReleaseInfo release, IProgress<double>? progress, CancellationToken cancellationToken);
}
