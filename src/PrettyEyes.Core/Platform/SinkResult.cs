namespace PrettyEyes.Core.Platform;

/// <summary>
/// Three outcomes, not two: cancelling a dialog is a normal user action and
/// must not look like a failure, but the caller has to tell them apart -
/// cancel returns to the overlay silently, failure shows a message.
/// </summary>
public enum SinkResult
{
    Sent,
    Cancelled,
    Failed,
}
