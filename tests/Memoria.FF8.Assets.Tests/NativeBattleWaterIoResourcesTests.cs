using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleWaterIoResourcesTests
{
    [Fact]
    public void Original_three_external_files_preserve_exact_directory_names_and_complete_bytes()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original resources are optional.");
        using GameAssets game = new(directory);
        using BattleAssets battle = new(directory);
        NativeBattleWaterIoResources resources = game.ReadNativeWaterIoResources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeWaterIoResources());
        Assert.Equal(new ushort[] { 363, 364, 365 }, resources.Files.Select(file => file.FileId));
        Assert.Equal(new[] { "ma8def_p.0", "ma8def_p.1", "ma8def_p.2" }, resources.Files.Select(file => file.Name));
        Assert.All(resources.Files, file =>
        {
            NativeBattleArchiveFile current = battle.ReadNativeBattleFile(file.FileId);
            Assert.Equal(0x8000, file.Bytes.Length);
            Assert.Equal(current.Name, file.Name);
            Assert.Equal(current.Bytes.ToArray(), file.Bytes.ToArray());
        });
        NativeBattleWaterIoResources restored = JsonSerializer.Deserialize<NativeBattleWaterIoResources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        for (int index = 0; index < 3; index++)
        {
            Assert.Equal(SHA256.HashData(resources.Files[index].Bytes.AsSpan()), SHA256.HashData(restored.Files[index].Bytes.AsSpan()));
        }
    }

    [Fact]
    public void External_file_profile_rejects_missing_wrong_order_wrong_names_and_partial_files()
    {
        ImmutableArray<NativeBattleArchiveFile> files = Enumerable.Range(0, 3).Select(index => new NativeBattleArchiveFile((ushort)(363 + index), $"ma8def_p.{index}", new byte[0x8000].ToImmutableArray())).ToImmutableArray();
        NativeBattleWaterIoResources resources = new(files);
        resources.Validate();
        Assert.Throws<InvalidDataException>(() => (resources with { Files = files.RemoveAt(2) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Files = files.SetItem(0, files[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Files = files.SetItem(1, files[1] with { Name = "other" }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Files = files.SetItem(2, files[2] with { Bytes = files[2].Bytes.RemoveAt(0) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Files = default }).Validate());
        System.Text.Json.Nodes.JsonObject missing = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(resources))!.AsObject();
        missing.Remove(nameof(NativeBattleWaterIoResources.Files));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleWaterIoResources>(missing.ToJsonString()));
    }
}
