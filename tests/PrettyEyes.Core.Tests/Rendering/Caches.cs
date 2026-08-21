using PrettyEyes.Core.Rendering;

namespace PrettyEyes.Core.Tests.Rendering;

/// <summary>
/// A cache to hand to annotations that never look at it. Everything except the
/// blur ignores the parameter, and spelling out a fresh instance at every call
/// site would say something the test does not mean.
/// </summary>
internal static class Caches
{
    /// <summary>Never read, never written, never disposed. That is the point.</summary>
    public static readonly BlurCache Unused = new();
}
