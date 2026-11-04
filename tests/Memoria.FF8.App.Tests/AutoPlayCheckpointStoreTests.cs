using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayCheckpointStoreTests
{
    [Fact]
    public void InPlaceConversionAndSaveTemporarySourcePreserveTheirPayloads()
    {
        using TestDirectory files = new();
        string stored = files.PathFor("stored.json");
        GameSession session = GameSession.CreateColdProcess();
        session.Save(stored, compact: true);
        string expected = JsonSerializer.Serialize(GameSession.Load(stored));
        AutoPlayCheckpointStore.Write(stored, stored);
        Assert.True(AutoPlayCheckpointStore.IsStored(stored));
        Assert.Equal(expected, JsonSerializer.Serialize(AutoPlayCheckpointStore.Load(stored)));
        AutoPlayCheckpointStore.Write(stored, stored);
        Assert.Equal(expected, JsonSerializer.Serialize(AutoPlayCheckpointStore.Load(stored)));
        string temporarySource = stored + ".tmp";
        session.Save(temporarySource, compact: true);
        byte[] sourceBytes = File.ReadAllBytes(temporarySource);
        AutoPlayCheckpointStore.Write(temporarySource, stored);
        Assert.Equal(sourceBytes, File.ReadAllBytes(temporarySource));
        Assert.Equal(expected, JsonSerializer.Serialize(AutoPlayCheckpointStore.Load(stored)));
        Assert.Empty(Directory.GetFiles(files.Directory, "*.index-*.tmp"));
    }

    [Fact]
    public void FailedInPlaceConversionKeepsTheOriginalStandaloneSave()
    {
        using TestDirectory files = new();
        string source = files.PathFor("standalone.json");
        GameSession.CreateColdProcess().Save(source, compact: true);
        byte[] previous = File.ReadAllBytes(source);
        Assert.Throws<IOException>(() => AutoPlayCheckpointStore.Write(source, source, maxBlockBytes: 0));
        Assert.Equal(previous, File.ReadAllBytes(source));
        Assert.False(AutoPlayCheckpointStore.IsStored(source));
        GameSession.Load(source);
        Assert.Empty(Directory.GetFiles(files.Directory, "*.index-*.tmp"));
    }

    [Fact]
    public void CompactAndLegacySavesRoundTripWithTheSameStateAndProjection()
    {
        using TestDirectory files = new();
        GameState state = GameSession.CreateColdProcess().State with { FieldId = 130, X = 17, Y = 29 };
        GameSession session = new(state);
        foreach (bool compact in new[] { true, false })
        {
            string source = files.PathFor("source.json");
            string stored = files.PathFor("stored.json");
            session.Save(source, compact: compact);
            AutoPlayCheckpointStore.Write(source, stored);
            Assert.True(AutoPlayCheckpointStore.IsStored(stored));
            Assert.False(AutoPlayCheckpointStore.IsStored(source));
            Assert.Equal(JsonSerializer.Serialize(GameSession.Load(source)), JsonSerializer.Serialize(AutoPlayCheckpointStore.Load(stored)));
            Assert.Equal(130, AutoPlayCheckpointStore.ReadProjection<Projection>(stored).FieldId);
            Assert.Equal(130, AutoPlayCheckpointStore.Load(source).FieldId);
            Assert.Empty(Directory.GetFiles(files.Directory, ".checkpoint-read-*"));
        }
    }

    [Fact]
    public void IdenticalPayloadBlocksAreSharedAndStoredCheckpointsImportToAnotherDirectory()
    {
        using TestDirectory files = new();
        using TestDirectory imported = new();
        string source = files.PathFor("source.json");
        File.WriteAllText(source, JsonSerializer.Serialize(new { FieldId = 42, Padding = new string('a', 150000) }));
        AutoPlayCheckpointStore.Write(source, files.PathFor("first.json"));
        string[] originalBlocks = Directory.GetFiles(files.Blocks);
        Assert.True(ReadHashes(files.PathFor("first.json")).Length >= 2);
        AutoPlayCheckpointStore.Write(source, files.PathFor("second.json"));
        Assert.Equal(originalBlocks.Order(), Directory.GetFiles(files.Blocks).Order());
        AutoPlayCheckpointStore.Write(files.PathFor("first.json"), imported.PathFor("copied.json"));
        Assert.Equal(42, AutoPlayCheckpointStore.ReadProjection<Projection>(imported.PathFor("copied.json")).FieldId);
        Assert.Equal(originalBlocks.Select(System.IO.Path.GetFileName).Order(), Directory.GetFiles(imported.Blocks).Select(System.IO.Path.GetFileName).Order());
    }

    [Fact]
    public void ContentBoundariesShareTheSuffixAfterAnEarlierValueChangesLength()
    {
        using TestDirectory files = new();
        byte[] random = new byte[500000];
        new Random(12345).NextBytes(random);
        string padding = Convert.ToBase64String(random);
        string source = files.PathFor("source.json");
        File.WriteAllText(source, JsonSerializer.Serialize(new { FieldId = 1, Padding = padding }));
        AutoPlayCheckpointStore.Write(source, files.PathFor("first.json"));
        string[] first = ReadHashes(files.PathFor("first.json"));
        File.WriteAllText(source, JsonSerializer.Serialize(new { FieldId = 100000, Padding = padding }));
        AutoPlayCheckpointStore.Write(source, files.PathFor("second.json"));
        string[] second = ReadHashes(files.PathFor("second.json"));
        Assert.True(first.Intersect(second).Count() >= first.Length / 2);
        Assert.Equal(100000, AutoPlayCheckpointStore.ReadProjection<Projection>(files.PathFor("second.json")).FieldId);
    }

    [Fact]
    public void CorruptCompactSourceDoesNotReplaceACommittedIndex()
    {
        using TestDirectory files = new();
        string source = files.PathFor("source.json");
        string stored = files.PathFor("stored.json");
        GameSession.CreateColdProcess().Save(source, compact: true);
        AutoPlayCheckpointStore.Write(source, stored);
        byte[] previous = File.ReadAllBytes(stored);
        byte[] compact = File.ReadAllBytes(source);
        compact[^1] ^= 1;
        File.WriteAllBytes(source, compact);
        Assert.Throws<InvalidDataException>(() => AutoPlayCheckpointStore.Write(source, stored));
        Assert.Equal(previous, File.ReadAllBytes(stored));
    }

    [Fact]
    public void CollectionKeepsSharedBlocksAndDeletesOnlyUnreferencedManagedBlocks()
    {
        using TestDirectory files = new();
        string source = files.PathFor("source.json");
        File.WriteAllText(source, "{\"FieldId\":1}");
        AutoPlayCheckpointStore.Write(source, files.PathFor("old.json"));
        File.WriteAllText(source, "{\"FieldId\":2}");
        AutoPlayCheckpointStore.Write(source, files.PathFor("current.json"));
        AutoPlayCheckpointStore.Write(source, files.PathFor("shared.json"));
        string unrelated = System.IO.Path.Combine(files.Blocks, "other.br");
        File.WriteAllText(unrelated, "Unmanaged file");
        AutoPlayCheckpointStore.Collect(files.Directory, ["current.json", "shared.json"]);
        Assert.Equal(2, Directory.GetFiles(files.Blocks).Length);
        Assert.True(File.Exists(unrelated));
        Assert.Equal(2, AutoPlayCheckpointStore.ReadProjection<Projection>(files.PathFor("shared.json")).FieldId);
        Assert.Throws<FileNotFoundException>(() => AutoPlayCheckpointStore.ReadProjection<Projection>(files.PathFor("old.json")));
    }

    [Fact]
    public void CorruptionAndTraversalFailWithoutCollectingOtherBlocks()
    {
        using TestDirectory files = new();
        string source = files.PathFor("source.json");
        string stored = files.PathFor("stored.json");
        File.WriteAllText(source, "{\"FieldId\":42}");
        AutoPlayCheckpointStore.Write(source, stored);
        string block = Assert.Single(Directory.GetFiles(files.Blocks));
        File.WriteAllBytes(block, [0]);
        Assert.ThrowsAny<IOException>(() => AutoPlayCheckpointStore.ReadProjection<Projection>(stored));
        Assert.Empty(Directory.GetFiles(files.Directory, ".checkpoint-read-*"));
        Assert.Throws<InvalidDataException>(() => AutoPlayCheckpointStore.Collect(files.Directory, ["../outside.json"]));
        Assert.True(File.Exists(block));
        Assert.ThrowsAny<IOException>(() => AutoPlayCheckpointStore.Collect(files.Directory, ["stored.json"]));
        Assert.True(File.Exists(block));
        File.WriteAllText(stored, "{\"AutoPlayCheckpoint\":1,\"Length\":1,\"Blocks\":[{\"Hash\":\"../outside\",\"Length\":1}]}");
        Assert.Throws<InvalidDataException>(() => AutoPlayCheckpointStore.ReadProjection<Projection>(stored));
    }

    [Fact]
    public void CaptureBudgetPreservesThePreviouslyCommittedCheckpoint()
    {
        using TestDirectory files = new();
        string source = files.PathFor("source.json");
        string stored = files.PathFor("stored.json");
        File.WriteAllText(source, "{\"FieldId\":1}");
        AutoPlayCheckpointStore.Write(source, stored);
        byte[] previous = File.ReadAllBytes(stored);
        File.WriteAllText(source, "{\"FieldId\":2}");
        IOException failure = Assert.Throws<IOException>(() => AutoPlayCheckpointStore.Write(source, stored, maxBlockBytes: 1));
        Assert.Contains("capture budget", failure.Message);
        Assert.Equal(previous, File.ReadAllBytes(stored));
        Assert.Equal(1, AutoPlayCheckpointStore.ReadProjection<Projection>(stored).FieldId);
        Assert.Empty(Directory.GetFiles(files.Blocks, "*.tmp"));
    }

    private static string[] ReadHashes(string path)
    {
        using JsonDocument index = JsonDocument.Parse(File.ReadAllText(path));
        return index.RootElement.GetProperty("Blocks").EnumerateArray().Select(block => block.GetProperty("Hash").GetString()!).ToArray();
    }

    private sealed record Projection(int FieldId);

    private sealed class TestDirectory : IDisposable
    {
        public string Directory { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "autoplay-checkpoint-tests-" + Guid.NewGuid().ToString("N"));
        public string Blocks => System.IO.Path.Combine(Directory, ".checkpoint-blocks");

        public TestDirectory()
        {
            System.IO.Directory.CreateDirectory(Directory);
        }

        public string PathFor(string name) => System.IO.Path.Combine(Directory, name);

        public void Dispose()
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
