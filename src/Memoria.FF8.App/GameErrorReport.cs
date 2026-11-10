using Memoria.FF7.Runtime;
using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class GameErrorReport
{
    internal static Task<PreparedErrorReport> PrepareAsync(ErrorIncident incident, string? reportsDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(incident);
        if (incident.Save is { } save)
        {
            using MemoryStream source = new(save.CopyBytes(), writable: false);
            if (CombinedSessionCodec.HasSignature(source))
            {
                return Ff7ErrorReport.PrepareAsync(incident, reportsDirectory);
            }
        }
        return RuntimeErrorReport.PrepareAsync(incident, reportsDirectory);
    }
}
