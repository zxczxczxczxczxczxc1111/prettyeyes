using PrettyEyes.Core.Geometry;

namespace PrettyEyes.Core.Platform;

public interface IMonitorEnumerator
{
    DesktopLayout Enumerate();
}
