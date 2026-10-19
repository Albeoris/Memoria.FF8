using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class PlayerSaveTimestampTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-save-timestamps-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_entries_mix_every_quick_save_with_dated_slots_before_empty_slots()
    {
        PlayerSaveStore store = CreateStore();
        GameSession session = new(new GameState { Scene = SceneKind.Field });
        session.Save(store.GetPath(2));
        DateTime timestamp = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(store.GetPath(2), timestamp);
        store.Save(0, session);
        string olderQuick = store.GetPath(0);
        File.SetLastWriteTimeUtc(olderQuick, timestamp.AddMinutes(1));
        store.Save(0, session);
        File.SetLastWriteTimeUtc(store.GetPath(0), timestamp.AddMinutes(3));
        File.WriteAllText(store.GetPath(4), "{invalid");
        File.SetLastWriteTimeUtc(store.GetPath(4), timestamp.AddMinutes(2));

        IReadOnlyList<PlayerSaveEntry> entries = store.ReadEntries(true);

        Assert.Equal(102, entries.Count);
        Assert.Equal(new[] { 0, 4, -1, 2, 1, 3, 5 }, entries.Take(7).Select(entry => entry.Slot));
        Assert.False(entries[1].CanLoad);
        Assert.NotNull(entries[1].SavedAtUtc);
        Assert.Equal(Enumerable.Range(1, 100), store.ReadEntries(false).Select(entry => entry.Slot));
        Assert.Equal(olderQuick, store.GetPath(entries[2].Slot));
    }

    [Fact]
    public void Saved_wall_time_keeps_its_original_offset_when_file_dates_change_and_entries_are_cached()
    {
        PlayerSaveStore store = CreateStore();
        store.Save(1, new GameSession(new GameState { Scene = SceneKind.Field }));
        DateTimeOffset original = new(2026, 2, 3, 21, 42, 10, TimeSpan.FromHours(-7));
        RewriteSavedLocalAt(store.GetPath(1), original);
        File.SetLastWriteTimeUtc(store.GetPath(1), new DateTime(2026, 2, 4, 15, 0, 0, DateTimeKind.Utc));

        PlayerSaveEntry first = store.ReadEntries(false)[0];
        PlayerSaveEntry cached = store.ReadEntries(false)[0];

        Assert.Equal(original, first.SavedLocalAt);
        Assert.Equal(original.Offset, first.SavedLocalAt!.Value.Offset);
        Assert.Equal("2026-02-03 21:42:10", first.SavedAtText);
        Assert.Equal(first, cached);
        Assert.Equal(File.GetLastWriteTime(store.GetPath(1)), first.SavedAt);
        Assert.Equal(File.GetLastWriteTimeUtc(store.GetPath(1)), first.SavedAtUtc);
        File.SetLastWriteTimeUtc(store.GetPath(1), first.SavedAtUtc!.Value.AddDays(1));
        Assert.Equal("2026-02-03 21:42:10", store.ReadEntries(false)[0].SavedAtText);
        Assert.Equal("2026-02-03 21:42:10", CreateStore().ReadEntries(false)[0].SavedAtText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Legacy_json_and_older_compact_saves_fall_back_to_the_file_local_time(bool compact)
    {
        PlayerSaveStore store = CreateStore();
        string path = store.GetPath(1);
        new GameSession(new GameState { Scene = SceneKind.Field }).Save(path, compact);
        if (compact)
        {
            RewriteSavedLocalAt(path, null);
        }
        File.SetLastWriteTimeUtc(path, new DateTime(2020, 8, 9, 10, 11, 12, DateTimeKind.Utc));

        PlayerSaveEntry entry = store.ReadEntries(false)[0];

        Assert.True(entry.CanLoad);
        Assert.Null(entry.SavedLocalAt);
        Assert.Equal(File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), entry.SavedAtText);
        Assert.Equal(SceneKind.Field, store.Load(1).Scene);
        Assert.Equal("", store.ReadEntries(false)[1].SavedAtText);
    }

    [Fact]
    public void Valid_display_timestamp_remains_available_when_location_data_is_unavailable()
    {
        PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "saves"), _ => throw new InvalidDataException("Location data unavailable."));
        store.Save(1, new GameSession(new GameState { Scene = SceneKind.Field }));
        DateTimeOffset original = new(2026, 2, 3, 21, 42, 10, TimeSpan.FromHours(9));
        RewriteSavedLocalAt(store.GetPath(1), original);

        PlayerSaveEntry entry = store.ReadEntries(false)[0];

        Assert.False(entry.CanLoad);
        Assert.Equal(original, entry.SavedLocalAt);
        Assert.Equal("2026-02-03 21:42:10", entry.SavedAtText);
    }

    private static void RewriteSavedLocalAt(string path, DateTimeOffset? savedLocalAt)
    {
        byte[] bytes = File.ReadAllBytes(path);
        int length = BitConverter.ToInt32(bytes, 12);
        JsonNode summary = JsonNode.Parse(bytes.AsSpan(88, length))!;
        summary.AsObject().Remove("SavedLocalAt");
        if (savedLocalAt is { } timestamp)
        {
            summary["SavedLocalAt"] = JsonSerializer.SerializeToNode(timestamp);
        }
        byte[] updated = JsonSerializer.SerializeToUtf8Bytes(summary);
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write);
        stream.Write(bytes.AsSpan(0, 12));
        stream.Write(BitConverter.GetBytes(updated.Length));
        stream.Write(bytes.AsSpan(16, 8));
        stream.Write(SHA256.HashData(updated));
        stream.Write(bytes.AsSpan(56, 32));
        stream.Write(updated);
        stream.Write(bytes.AsSpan(88 + length));
    }

    private PlayerSaveStore CreateStore()
    {
        return new PlayerSaveStore(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "saves"));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
