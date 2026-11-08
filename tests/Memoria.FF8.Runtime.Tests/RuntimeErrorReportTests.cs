using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class RuntimeErrorReportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "memoria-portable-report-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Legacy_report_snapshot_is_migrated_without_changing_capture_metadata_or_gameplay(bool compact, bool missingLocalTime)
    {
        Directory.CreateDirectory(directory);
        GameState plain = new() { Scene = SceneKind.Field, FieldId = 165, X = 25, MovieSource = new(1, 7) };
        JsonObject vm = JsonSerializer.SerializeToNode(plain)!.AsObject();
        vm["Scene"] = JsonSerializer.SerializeToNode(new { Names = new Dictionary<int, string> { [0] = @"C:\Hero", [1] = "x@y.example" } });
        vm["LastFault"] = JsonSerializer.SerializeToNode(new { FieldId = 165, ObjectId = 3, ScriptId = 2, Operation = 7, Opcode = "MOVIEREADY", Message = @"Cannot open C:\TestProfiles\private-user\movie.avi" });
        vm["MovieSource"]!.AsObject()["Path"] = @"C:\TestProfiles\private-user\movie.avi";
        OriginalAchievementObservedEvent observation = OriginalAchievementObservedEvent.Create(new() { MagazineCollection = true }, new("provider", "product", "real-account", "private-profile"));
        GameState legacy = plain with { EventState = JsonSerializer.SerializeToElement(vm), LastOriginalAchievementObservation = observation };
        DateTimeOffset? localTime = missingLocalTime ? null : new DateTimeOffset(2021, 2, 3, 4, 5, 6, TimeSpan.FromHours(-5));
        string originalPath = Path.Combine(directory, "legacy.save");
        using (FileStream source = File.Create(originalPath))
        {
            if (compact)
            {
                SnapshotStorage.Write(source, legacy, new JsonSerializerOptions(), localTime, preserveSavedLocalAt: true);
            }
            else
            {
                JsonSerializer.Serialize(source, legacy);
            }
        }
        byte[] original = File.ReadAllBytes(originalPath);
        DateTimeOffset captured = new(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
        ReportSave save = new(original, "recovered snapshot", captured, "validated original evidence");
        ErrorIncident incident = ErrorIncident.Capture(new Exception("failed"), "test", save: save, launchId: "original-launch");
        string reports = Path.Combine(directory, "reports");
        using PreparedErrorReport report = await RuntimeErrorReport.PrepareAsync(incident, reports);
        using (ZipArchive archive = ZipFile.OpenRead(report.ArchivePath))
        {
            ZipArchiveEntry entry = Assert.Single(archive.Entries, entry => entry.FullName.StartsWith("repro/", StringComparison.Ordinal));
            string attached = Path.Combine(directory, "attached.save");
            entry.ExtractToFile(attached);
            GameState loaded = GameSession.Load(attached);
            Assert.Equal(plain.FieldId, loaded.FieldId);
            Assert.Equal(plain.X, loaded.X);
            Assert.Null(loaded.LastOriginalAchievementObservation!.Value.Owner);
            Assert.Equal(OriginalAchievementObservedEvent.Create(observation.Progress, null), loaded.LastOriginalAchievementObservation);
            JsonElement savedVm = loaded.EventState!.Value;
            Assert.DoesNotContain("private-user", savedVm.GetRawText());
            Assert.Equal(7, savedVm.GetProperty("LastFault").GetProperty("Operation").GetInt32());
            Assert.True(JsonElement.DeepEquals(legacy.EventState!.Value.GetProperty("Scene"), savedVm.GetProperty("Scene")));
            if (compact)
            {
                Assert.Equal(localTime, GameSession.ReadSummary(attached)!.SavedLocalAt);
                Assert.Equal(localTime?.Offset, GameSession.ReadSummary(attached)!.SavedLocalAt?.Offset);
            }
            using Stream metadata = archive.GetEntry("incident.json")!.Open();
            using JsonDocument evidence = await JsonDocument.ParseAsync(metadata, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(incident.Timestamp, evidence.RootElement.GetProperty("Timestamp").GetDateTimeOffset());
            Assert.Equal(incident.LaunchId, evidence.RootElement.GetProperty("LaunchId").GetString());
            JsonElement savedMetadata = evidence.RootElement.GetProperty("Save");
            Assert.Equal(save.Kind, savedMetadata.GetProperty("Kind").GetString());
            Assert.Equal(save.CapturedUtc, savedMetadata.GetProperty("CapturedUtc").GetDateTimeOffset());
            Assert.Equal(save.Validation, savedMetadata.GetProperty("Validation").GetString());
        }
        Assert.Equal(original, File.ReadAllBytes(originalPath));
        Assert.Equal(original, save.CopyBytes());
        Assert.Single(Directory.EnumerateFiles(reports, "*.zip", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Invalid_legacy_bytes_fail_before_publishing_a_report()
    {
        ErrorIncident incident = ErrorIncident.Capture(new Exception("failed"), "test", save: new ReportSave([1, 2, 3], "invalid", DateTimeOffset.UtcNow, "claimed validation"));
        await Assert.ThrowsAnyAsync<Exception>(() => RuntimeErrorReport.PrepareAsync(incident, directory));
        Assert.False(Directory.Exists(directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
