using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotStorageTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-save-format-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Compressed_snapshot_roundtrips_physical_memory_and_exposes_small_display_header()
    {
        Directory.CreateDirectory(directory);
        byte[] bytes = Enumerable.Range(0, 32768).Select(value => (byte)value).ToArray();
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.FromColdProcess() with { FileStaging = NativeBattleFileStaging.Unknown().Write(NativeBattleFileStaging.Address, bytes) };
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
    }

    [Fact]
    public void Truncated_payload_is_rejected_by_display_and_full_read()
    {
        string path = CreateSave();
        byte[] bytes = File.ReadAllBytes(path);
        File.WriteAllBytes(path, bytes[..^1]);
        Assert.Throws<InvalidDataException>(() => GameSession.ReadSummary(path));
        Assert.Throws<InvalidDataException>(() => GameSession.Load(path));
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
