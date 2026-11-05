using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotStorageTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-save-format-" + Guid.NewGuid().ToString("N"));

    private sealed record SnapshotProjection([property: System.Text.Json.Serialization.JsonRequired] int FieldId);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Exact_publication_preserves_existing_save_and_foreign_temporary_on_validation_failure(bool compact)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "atomic.save");
        string foreign = path + ".tmp";
        byte[] original = [1, 2, 3];
        byte[] foreignBytes = [4, 5, 6];
        File.WriteAllBytes(path, original);
        File.WriteAllBytes(foreign, foreignBytes);
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 165 });
        Assert.Throws<InvalidDataException>(() => session.Save(path, compact, candidate =>
        {
            Assert.Equal(165, candidate.FieldId);
            throw new InvalidDataException("Rejected continuation.");
        }));
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal(foreignBytes, File.ReadAllBytes(foreign));
        Assert.Equal(2, Directory.GetFiles(directory).Length);
        session.Save(path, compact);
        Assert.Equal(165, GameSession.Load(path).FieldId);
        Assert.Equal(foreignBytes, File.ReadAllBytes(foreign));
        Assert.Equal(2, Directory.GetFiles(directory).Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Projection_reads_metadata_without_requiring_complete_gameplay_owners(bool compact)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "projection.json");
        using (FileStream stream = File.Create(path))
        {
            GameState state = new() { FieldId = 159 };
            if (compact)
            {
                SnapshotStorage.Write(stream, state, new JsonSerializerOptions());
            }
            else
            {
                JsonSerializer.Serialize(stream, new { FieldId = state.FieldId });
            }
        }
        Assert.Equal(159, GameSession.ReadSnapshotProjection<SnapshotProjection>(path).FieldId);
    }

    [Fact]
    public void Compact_save_records_the_local_wall_time_and_original_offset_in_display_metadata()
    {
        DateTimeOffset before = DateTimeOffset.Now;
        string path = CreateSave();
        DateTimeOffset after = DateTimeOffset.Now;

        SaveSummary summary = Assert.IsType<SaveSummary>(GameSession.ReadSummary(path));
        DateTimeOffset timestamp = Assert.IsType<DateTimeOffset>(summary.SavedLocalAt);

        Assert.InRange(timestamp.UtcDateTime, before.UtcDateTime, after.UtcDateTime);
        Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(timestamp), timestamp.Offset);
        Assert.Equal(timestamp, GameSession.ReadSummary(path)!.SavedLocalAt);
        Assert.Equal(SceneKind.Field, GameSession.Load(path).Scene);
    }

    [Fact]
    public void Compressed_snapshot_roundtrips_physical_memory_and_exposes_small_display_header()
    {
        Directory.CreateDirectory(directory);
        byte[] bytes = Enumerable.Range(0, 32768).Select(value => (byte)value).ToArray();
        NativeBattleRetainedMemory retained = NativeBattleStorageSnapshots.Capture(NativeBattleRetainedMemory.FromColdProcess() with { FileStaging = NativeBattleFileStaging.Unknown().Write(NativeBattleFileStaging.Address, bytes) });
        GameState state = new() { Scene = SceneKind.Field, FieldId = 180, FieldName = "bghoke_2", X = 12.5f, NativeBattleMemory = retained, Playtime = new(90061, 0, 0), ExamTimer = NativeExamTimerState.FromColdProcess() };
        GameSession session = new(state);
        string json = Path.Combine(directory, "legacy.json");
        string packed = Path.Combine(directory, "packed.json");
        session.Save(json);
        session.Save(packed, compact: true);
        Assert.Null(GameSession.ReadSummary(json));
        SaveSummary summary = Assert.IsType<SaveSummary>(GameSession.ReadSummary(packed));
        Assert.Equal(180, summary.FieldId);
        Assert.Equal(90061u, summary.Playtime!.Seconds);
        GameState restored = GameSession.Load(packed);
        Assert.Equal(JsonSerializer.Serialize(GameSession.Load(json)), JsonSerializer.Serialize(restored));
        Assert.Equal(bytes, restored.NativeBattleMemory!.FileStaging!.Read(NativeBattleFileStaging.Address, bytes.Length));
        Assert.True(new FileInfo(packed).Length < new FileInfo(json).Length / 4);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(24)]
    [InlineData(88)]
    public void Invalid_header_is_rejected_without_loading_payload(int offset)
    {
        string path = CreateSave();
        byte[] bytes = File.ReadAllBytes(path);
        bytes[offset] ^= 1;
        File.WriteAllBytes(path, bytes);
        Assert.Throws<InvalidDataException>(() => GameSession.ReadSummary(path));
        Assert.Throws<InvalidDataException>(() => GameSession.Load(path));
        Assert.Throws<InvalidDataException>(() => GameSession.ReadSnapshotProjection<SnapshotProjection>(path));
    }

    [Fact]
    public void Modified_payload_is_rejected_even_when_gzip_data_remains_valid()
    {
        string path = CreateSave();
        byte[] bytes = File.ReadAllBytes(path);
        int summaryLength = BitConverter.ToInt32(bytes, 12);
        // Gzip MTIME is ignored by the decoder but is covered by the container checksum.
        bytes[88 + summaryLength + 4] ^= 1;
        File.WriteAllBytes(path, bytes);
        Assert.NotNull(GameSession.ReadSummary(path));
        Assert.Throws<InvalidDataException>(() => GameSession.Load(path));
        Assert.Throws<InvalidDataException>(() => GameSession.ReadSnapshotProjection<SnapshotProjection>(path));
    }

    [Fact]
    public void Truncated_payload_is_rejected_by_display_and_full_read()
    {
        string path = CreateSave();
        byte[] bytes = File.ReadAllBytes(path);
        File.WriteAllBytes(path, bytes[..^1]);
        Assert.Throws<InvalidDataException>(() => GameSession.ReadSummary(path));
        Assert.Throws<InvalidDataException>(() => GameSession.Load(path));
        Assert.Throws<InvalidDataException>(() => GameSession.ReadSnapshotProjection<SnapshotProjection>(path));
    }

    [Fact]
    public void Byte_buffer_converter_reads_legacy_arrays_and_writes_base64()
    {
        JsonSerializerOptions options = new() { Converters = { new ImmutableBytesJsonConverter() } };
        ImmutableArray<byte> bytes = JsonSerializer.Deserialize<ImmutableArray<byte>>("[0,128,255]", options);
        Assert.Equal(new byte[] { 0, 128, 255 }, bytes.ToArray());
        string encoded = JsonSerializer.Serialize(bytes, options);
        Assert.Equal("\"AID/\"", encoded);
        Assert.Equal(bytes.ToArray(), JsonSerializer.Deserialize<ImmutableArray<byte>>(encoded, options).ToArray());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ImmutableArray<byte>>("[256]", options));
    }

    [Fact]
    public void Physical_region_encoding_round_trips_with_default_options_and_legacy_numeric_arrays()
    {
        NativeBattleEffectMemoryRegion original = new(17, ImmutableArray.Create<byte>(0, 128, 255));
        string encoded = JsonSerializer.Serialize(original);
        using JsonDocument document = JsonDocument.Parse(encoded);
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("Data").ValueKind);
        NativeBattleEffectMemoryRegion restored = JsonSerializer.Deserialize<NativeBattleEffectMemoryRegion>(encoded)!;
        Assert.Equal(original.Offset, restored.Offset);
        Assert.Equal(original.Data.ToArray(), restored.Data.ToArray());
        NativeBattleEffectMemoryRegion legacy = JsonSerializer.Deserialize<NativeBattleEffectMemoryRegion>("{\"Offset\":17,\"Data\":[0,128,255]}")!;
        Assert.Equal(original.Data.ToArray(), legacy.Data.ToArray());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleEffectMemoryRegion>("{\"Offset\":17,\"Data\":[256]}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(new NativeBattleEffectMemoryRegion(0, default)));
    }

    private string CreateSave()
    {
        string path = Path.Combine(directory, "snapshot.json");
        new GameSession(new GameState { Scene = SceneKind.Field }).Save(path, compact: true);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
