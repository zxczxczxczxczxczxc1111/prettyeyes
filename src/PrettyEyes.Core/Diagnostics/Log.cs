using System.Diagnostics;

namespace PrettyEyes.Core.Diagnostics;

/// <summary>
/// The one place anything is written down. A tray application shows nothing on
/// screen when it fails, so without a file on disk the first crash on somebody
/// else's machine is unexplainable.
///
/// The path is a constructor argument rather than a constant: a log that can
/// only write into the real user profile cannot be tested, and untested
/// logging tends to be the thing that throws during a crash.
/// </summary>
public sealed class Log
{
    private const long DefaultMaxBytes = 512 * 1024;

    private static Log? _default;

    private readonly object _gate = new();
    private readonly string _path;
    private readonly long _maxBytes;

    public Log(string path, long maxBytes = DefaultMaxBytes)
    {
        _path = path;
        _maxBytes = maxBytes;
    }

    /// <summary>%APPDATA%\prettyeyes\log.txt, next to settings.json.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "prettyeyes",
        "log.txt");

    /// <summary>The instance the application uses; tests build their own.</summary>
    public static Log Default => _default ??= new Log(DefaultPath);

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception error) =>
        Write("ERROR", $"{message}: {error.GetType().Name}: {error.Message}{Environment.NewLine}{error.StackTrace}");

    /// <summary>
    /// Times a piece of work and writes down how long it took. Used for the
    /// numbers the performance work is judged by, so it has to be cheap enough
    /// to leave in place.
    /// </summary>
    public IDisposable Scope(string name) => new Timed(this, name);

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}";

        lock (_gate)
        {
            try
            {
                var directory = Path.GetDirectoryName(_path);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                Roll();
                File.AppendAllLines(_path, [line]);
            }
            catch (IOException)
            {
                // Logging is not worth taking the application down for, and
                // there is nowhere left to report the failure to anyway.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// Keeps the current file and one previous. Two files bound the disk usage
    /// and still leave the run before the crash readable.
    /// </summary>
    private void Roll()
    {
        var file = new FileInfo(_path);

        if (!file.Exists || file.Length < _maxBytes)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_path) ?? string.Empty;
        var rolled = Path.Combine(
            directory,
            $"{Path.GetFileNameWithoutExtension(_path)}.1{Path.GetExtension(_path)}");

        File.Move(_path, rolled, overwrite: true);
    }

    private sealed class Timed : IDisposable
    {
        private readonly Log _log;
        private readonly string _name;
        private readonly Stopwatch _watch = Stopwatch.StartNew();

        public Timed(Log log, string name)
        {
            _log = log;
            _name = name;
        }

        public void Dispose()
        {
            _watch.Stop();
            _log.Info($"{_name}: {_watch.Elapsed.TotalMilliseconds:F1} мс");
        }
    }
}
