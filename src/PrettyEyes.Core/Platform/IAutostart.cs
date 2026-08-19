namespace PrettyEyes.Core.Platform;

public interface IAutostart
{
    bool IsEnabled { get; }

    /// <summary>
    /// Returns false when the switch could not be applied, so the UI can put
    /// the checkbox back instead of lying about the state.
    /// </summary>
    bool Set(bool enabled);
}
