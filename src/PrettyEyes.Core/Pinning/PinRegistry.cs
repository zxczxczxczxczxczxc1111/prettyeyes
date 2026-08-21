namespace PrettyEyes.Core.Pinning;

/// <summary>
/// A pinned screenshot, as far as anything outside the window cares.
/// </summary>
public interface IPinned
{
    /// <summary>
    /// Whether anything was drawn <b>in this window</b>. Annotations that came
    /// from the overlay are baked into the picture and are nobody's to lose;
    /// these are the ones a close would throw away.
    /// </summary>
    bool HasOwnAnnotations { get; }
}

/// <summary>
/// Who is pinned right now.
///
/// The half of the pin bookkeeping that knows nothing about windows, so it can
/// be tested: the test project references Core alone, and a registry living in
/// the app would be a registry no test can see.
/// </summary>
public sealed class PinRegistry
{
    private readonly List<IPinned> _pins = [];

    /// <summary>
    /// A copy, not the live list. Closing every pin means walking this while
    /// each close removes itself, and a live view would throw halfway through.
    /// </summary>
    public IReadOnlyList<IPinned> Pins => [.. _pins];

    public int Count => _pins.Count;

    /// <summary>
    /// Whether anything would be lost by closing everything. Asked before an
    /// update restarts the application.
    /// </summary>
    public bool AnyWithAnnotations => _pins.Any(pin => pin.HasOwnAnnotations);

    public void Add(IPinned pin) => _pins.Add(pin);

    /// <summary>False when it was not there: a second close is not an error.</summary>
    public bool Remove(IPinned pin) => _pins.Remove(pin);
}
