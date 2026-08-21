using System.Diagnostics;
using System.Reflection;
using Avalonia.Threading;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Updates;

namespace PrettyEyes.App.Services;

/// <summary>What the update is doing right now, for the one line that says so.</summary>
public enum UpdateStage
{
    Idle,
    Checking,
    UpToDate,
    Available,
    Downloading,
    Installing,
    Failed,
}

/// <summary>
/// The whole state in one value. Handed out rather than exposed as a handful of
/// properties: a status line built from three fields read at three moments can
/// show a combination that never existed.
/// </summary>
public sealed record UpdateState(UpdateStage Stage, ReleaseVersion? Version = null, double Progress = 0);

/// <summary>
/// Checks for a newer release, downloads it, and hands it to the installer.
///
/// Everything here runs on the UI thread apart from the network itself: the
/// state feeds a window and a tray menu, and a state machine driven from two
/// threads is a bug that only shows up on somebody else's machine.
/// </summary>
public sealed class UpdateService : IDisposable
{
    /// <summary>
    /// Not at start-up: the first thirty seconds belong to warming the capture
    /// and to whatever the user pressed the shortcut for.
    /// </summary>
    private static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    /// <summary>Inno Setup: no questions, close us, start us again afterwards.</summary>
    private const string InstallerArguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS";

    private readonly IUpdateSource _source;
    private readonly Func<bool> _enabled;
    private readonly Func<string?> _busy;
    private readonly DispatcherTimer _timer;

    private CancellationTokenSource? _work;
    private ReleaseInfo? _found;
    private bool _announced;

    /// <param name="busy">
    /// Why the installer must wait, or null when it may go ahead. A reason
    /// rather than a flag: there is more than one kind of work an update would
    /// throw away, and the log has to say which one it was.
    /// </param>
    public UpdateService(IUpdateSource source, Func<bool> enabled, Func<string?> busy)
    {
        _source = source;
        _enabled = enabled;
        _busy = busy;

        _timer = new DispatcherTimer { Interval = FirstCheck };
        _timer.Tick += (_, _) =>
        {
            // The first tick is the short one; every one after it is the daily
            // check, so the interval is moved once and left alone.
            _timer.Interval = Interval;
            _ = CheckAsync(manual: false);
        };
    }

    /// <summary>The version this build calls itself, from the assembly.</summary>
    public static ReleaseVersion Current { get; } = FromAssembly();

    public UpdateState State { get; private set; } = new(UpdateStage.Idle);

    public event EventHandler<UpdateState>? StateChanged;

    /// <summary>
    /// A newer version was found for the first time. Raised once per run: a
    /// notification every day about the same release is an advert.
    /// </summary>
    public event EventHandler<ReleaseVersion>? Announced;

    /// <summary>The release waiting to be installed, or null.</summary>
    public ReleaseInfo? Found => _found;

    public void Start()
    {
        if (_enabled())
        {
            _timer.Start();
        }
    }

    /// <summary>
    /// Called when the setting is switched. Turning it off also stops a check
    /// already on its way: the switch is an answer about now, not about later.
    /// </summary>
    public void Reschedule()
    {
        if (_enabled())
        {
            if (!_timer.IsEnabled)
            {
                _timer.Interval = FirstCheck;
                _timer.Start();
            }

            return;
        }

        _timer.Stop();
        Cancel();
        Publish(new UpdateState(UpdateStage.Idle));
    }

    public async Task CheckAsync(bool manual)
    {
        if (State.Stage is UpdateStage.Checking or UpdateStage.Downloading or UpdateStage.Installing)
        {
            return;
        }

        if (!manual && !_enabled())
        {
            return;
        }

        Publish(new UpdateState(UpdateStage.Checking));

        Cancel();
        _work = new CancellationTokenSource();

        var release = await _source.LatestAsync(_work.Token);

        if (release is null)
        {
            Log.Default.Info("проверка обновлений не удалась");
            Publish(new UpdateState(UpdateStage.Failed));

            return;
        }

        if (!release.Version.NewerThan(Current))
        {
            _found = null;
            Publish(new UpdateState(UpdateStage.UpToDate, Current));

            return;
        }

        _found = release;
        Log.Default.Info($"доступна версия {release.Version}");
        Publish(new UpdateState(UpdateStage.Available, release.Version));

        if (!_announced)
        {
            _announced = true;
            Announced?.Invoke(this, release.Version);
        }
    }

    /// <summary>
    /// Downloads and starts the installer. Returns true when the installer is
    /// running, which is the caller's signal to quit: the mutex has to be gone
    /// before Inno Setup gets to its own check for a running copy.
    /// </summary>
    public async Task<bool> InstallAsync()
    {
        if (_found is null || State.Stage is UpdateStage.Downloading or UpdateStage.Installing)
        {
            return false;
        }

        // The installer runs with /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS, and
        // a restart takes with it everything that exists nowhere else yet: a
        // selection in progress, and the drawings inside pinned windows.
        if (_busy() is { } reason)
        {
            Log.Default.Info($"обновление отложено: {reason}");

            return false;
        }

        var release = _found;

        Cancel();
        _work = new CancellationTokenSource();

        Publish(new UpdateState(UpdateStage.Downloading, release.Version));

        // Progress<T> posts every report to the UI thread, so the last few are
        // still in the queue when the download has already finished. Without
        // this guard they arrive after the final state and put the line back to
        // "Скачиваю 100%" forever.
        var progress = new Progress<double>(value =>
        {
            if (State.Stage == UpdateStage.Downloading)
            {
                Publish(new UpdateState(UpdateStage.Downloading, release.Version, value));
            }
        });

        var path = await _source.DownloadAsync(release, progress, _work.Token);

        if (path is null)
        {
            Log.Default.Error("установщик не скачался или не сошёлся хэш", new InvalidOperationException(release.AssetName));
            Publish(new UpdateState(UpdateStage.Failed, release.Version));

            return false;
        }

        Publish(new UpdateState(UpdateStage.Installing, release.Version));

        try
        {
            Process.Start(new ProcessStartInfo(path, InstallerArguments) { UseShellExecute = true });
            Log.Default.Info($"установщик {release.Version} запущен");

            return true;
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Log.Default.Error("установщик не запустился", error);
            Publish(new UpdateState(UpdateStage.Failed, release.Version));

            return false;
        }
    }

    public void Cancel()
    {
        _work?.Cancel();
        _work?.Dispose();
        _work = null;
    }

    public void Dispose()
    {
        _timer.Stop();
        Cancel();
        (_source as IDisposable)?.Dispose();
    }

    private void Publish(UpdateState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    /// <summary>
    /// The assembly carries 1.0.1.0; the fourth part is not part of a tag and
    /// is dropped rather than compared against something that never has one.
    /// </summary>
    private static ReleaseVersion FromAssembly()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;

        return version is null
            ? new ReleaseVersion(0, 0, 0)
            : new ReleaseVersion(version.Major, version.Minor, Math.Max(version.Build, 0));
    }
}
