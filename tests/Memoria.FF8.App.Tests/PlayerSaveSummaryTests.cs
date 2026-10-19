using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class PlayerSaveSummaryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-save-summary-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Menu_reads_the_summary_without_opening_the_compressed_payload_but_selected_load_validates_it()
    {
        PlayerSaveStore store = CreateStore(state => $"Field {state.FieldId}");
        store.Save(1, new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 180, Playtime = new NativePlaytimeState(90061, 0, 0), ExamTimer = NativeExamTimerState.FromColdProcess() }));
        string path = store.GetPath(1);
        byte[] bytes = File.ReadAllBytes(path);
        bytes[^1] ^= 0x80;
        File.WriteAllBytes(path, bytes);

        PlayerSaveEntry entry = store.ReadEntries(false)[0];

        Assert.True(entry.CanLoad);
        Assert.Equal("Field 180", entry.Details);
        Assert.Equal("25:01:01", entry.PlayTimeText);
        Assert.Throws<InvalidDataException>(() => store.Load(1));
    }

    [Fact]
    public void Legacy_metadata_is_cached_and_replacement_or_removal_refreshes_the_row()
    {
        int resolutions = 0;
        PlayerSaveStore store = CreateStore(state =>
        {
            resolutions++;
            return $"Field {state.FieldId}";
        });
        string path = store.GetPath(1);
        new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 180 }).Save(path);
        Assert.Null(GameSession.ReadSummary(path));

        Assert.Equal("Field 180", store.ReadEntries(false)[0].Details);
        Assert.Equal("Field 180", store.ReadEntries(false)[0].Details);
        Assert.Equal(1, resolutions);

        DateTime timestamp = File.GetLastWriteTimeUtc(path);
        new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 181 }).Save(path);
        File.SetLastWriteTimeUtc(path, timestamp.AddSeconds(2));
        Assert.Equal("Field 181", store.ReadEntries(false)[0].Details);
        Assert.Equal(2, resolutions);

        File.Delete(path);
        Assert.Equal("Empty", store.ReadEntries(false)[0].Details);
        new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 182 }).Save(path);
        Assert.Equal("Field 182", store.ReadEntries(false)[0].Details);
        Assert.Equal(3, resolutions);
    }

    [Fact]
    public void Unavailable_rows_are_cached_and_changed_files_are_retried()
    {
        int resolutions = 0;
        PlayerSaveStore store = CreateStore(state =>
        {
            resolutions++;
            if (state.FieldId == 180)
            {
                throw new InvalidDataException("Unavailable location data.");
            }
            return $"Field {state.FieldId}";
        });
        string path = store.GetPath(1);
        new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 180 }).Save(path);

        Assert.False(store.ReadEntries(false)[0].CanLoad);
        Assert.False(store.ReadEntries(false)[0].CanLoad);
        Assert.Equal(1, resolutions);

        new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 180, FieldName = "replacement" }).Save(path);
        Assert.False(store.ReadEntries(false)[0].CanLoad);
        Assert.Equal(2, resolutions);

        store.Save(1, new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 181 }));
        Assert.Equal("Field 181", store.ReadEntries(false)[0].Details);
        Assert.Equal(3, resolutions);
    }

    [Fact]
    public void Store_overwrite_invalidates_cached_metadata_even_if_file_stamp_is_preserved()
    {
        int resolutions = 0;
        PlayerSaveStore store = CreateStore(state =>
        {
            resolutions++;
            return $"Field {state.FieldId}";
        });
        var session = new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 180 });
        store.Save(1, session);
        string path = store.GetPath(1);
        DateTime timestamp = File.GetLastWriteTimeUtc(path);
        Assert.True(store.ReadEntries(false)[0].CanLoad);

        store.Save(1, session);
        File.SetLastWriteTimeUtc(path, timestamp);

        Assert.True(store.ReadEntries(false)[0].CanLoad);
        Assert.Equal(2, resolutions);
    }

    [Fact]
    public void Corrupt_summary_is_unavailable_and_valid_replacement_refreshes_it()
    {
        PlayerSaveStore store = CreateStore(state => $"Field {state.FieldId}");
        var session = new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 180 });
        store.Save(1, session);
        string path = store.GetPath(1);
        byte[] bytes = File.ReadAllBytes(path);
        bytes[24] ^= 0x80;
        File.WriteAllBytes(path, bytes);
        Assert.False(store.ReadEntries(false)[0].CanLoad);
        Assert.StartsWith("Unavailable:", store.ReadEntries(false)[0].Details);

        store.Save(1, session);

        Assert.Equal("Field 180", store.ReadEntries(false)[0].Details);
        Assert.True(store.ReadEntries(false)[0].CanLoad);
    }

    private PlayerSaveStore CreateStore(Func<GameState, string?> locationName)
    {
        return new PlayerSaveStore(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "saves"), locationName);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
