using Memoria.FF8.Platform;

namespace Memoria.FF8.Runtime;

/// <summary>Publishes portable snapshots through the current game-save contract before report packaging.</summary>
public static class RuntimeErrorReport
{
    public static async Task<PreparedErrorReport> PrepareAsync(ErrorIncident incident, string? reportsDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ErrorIncident portable = incident.Save is { } save ? await Task.Run(() => Normalize(incident, save)).ConfigureAwait(false) : incident;
        return await ErrorReport.PrepareAsync(portable, reportsDirectory).ConfigureAwait(false);
    }

    private static ErrorIncident Normalize(ErrorIncident incident, ReportSave save)
    {
        string cache = Path.GetFullPath(Path.Combine(ApplicationPaths.Current.Cache, "report-snapshots"));
        string directory = Path.Combine(cache, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            byte[] bytes = save.CopyBytes();
            bool compact = bytes.AsSpan().StartsWith("MFF8SAVE"u8);
            string source = Path.Combine(directory, "source.save");
            string destination = Path.Combine(directory, "portable.save");
            File.WriteAllBytes(source, bytes);
            DateTimeOffset? savedLocalAt = compact ? GameSession.ReadSummary(source)?.SavedLocalAt : null;
            GameSession session = new(GameSession.Load(source));
            session.SaveReportSnapshot(destination, compact, savedLocalAt);
            ReportSave portable = new(File.ReadAllBytes(destination), save.Kind, save.CapturedUtc, save.Validation);
            return incident.WithSave(portable);
        }
        finally
        {
            if (!Path.GetFullPath(directory).StartsWith(cache + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Report snapshot staging escaped its cache directory.");
            }
            Directory.Delete(directory, recursive: true);
        }
    }
}
