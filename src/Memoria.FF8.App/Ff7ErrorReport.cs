using Memoria.FF7.Runtime;
using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class Ff7ErrorReport
{
    internal static async Task<PreparedErrorReport> PrepareAsync(ErrorIncident incident, string? reportsDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ErrorIncident portable = incident.Save is { } save ? incident.WithSave(await Task.Run(() => NormalizeSave(save)).ConfigureAwait(false)) : incident;
        return await ErrorReport.PrepareAsync(portable, reportsDirectory).ConfigureAwait(false);
    }

    internal static ReportSave NormalizeSave(ReportSave save)
    {
        using MemoryStream source = new(save.CopyBytes(), writable: false);
        CombinedSessionSave combined = CombinedSessionCodec.Read(source);
        if (combined.Ff8Payload is { Length: > 0 } ff8)
        {
            ReportSave native = RuntimeErrorReport.NormalizeSave(new(ff8, save.Kind, save.CapturedUtc, save.Validation));
            combined = combined with { Ff8Payload = native.CopyBytes() };
        }
        using MemoryStream output = new();
        CombinedSessionCodec.Write(output, combined);
        return new(output.ToArray(), save.Kind, save.CapturedUtc, save.Validation, ".ffsave");
    }
}
