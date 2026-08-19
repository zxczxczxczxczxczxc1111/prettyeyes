namespace PrettyEyes.Core.Platform;

public interface IPointerLocation
{
    /// <summary>Cursor position in physical pixels of the virtual desktop.</summary>
    (int X, int Y) Current { get; }
}
