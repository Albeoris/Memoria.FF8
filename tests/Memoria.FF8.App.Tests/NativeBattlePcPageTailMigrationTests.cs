using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattlePcPageTailMigrationTests
{
    [Fact]
    public void Migrate_only_the_proved_static_read_tail_and_continue_the_retained_callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_PC_PAGE_TAIL_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_PC_PAGE_TAIL_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "Select the standalone page-capture boundary and a distinct migrated output.");
        Assert.NotEqual(Path.GetFullPath(source!), Path.GetFullPath(output!));
        Assert.False(File.Exists(output));
        byte[] original = File.ReadAllBytes(source!);
        string hash = Convert.ToHexString(SHA256.HashData(original));
        JsonNode migrated = JsonNode.Parse(original)!;
        Assert.Null(migrated["NativeEffectMemory"]!["Textures"]!["VramReadTail"]);
        migrated["NativeEffectMemory"]!["Textures"]!["VramReadTail"] = JsonSerializer.SerializeToNode(new byte[NativeBattleEffectTextureState.PcPageReadTailLength].ToImmutableArray());
        JsonNode reverted = migrated.DeepClone();
        reverted["NativeEffectMemory"]!["Textures"]!.AsObject().Remove("VramReadTail");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(original), reverted));
        string directory = Path.GetDirectoryName(Path.GetFullPath(output!))!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(output!, migrated.ToJsonString());
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(output!))!;
        BattleSession first = BattleSession.Restore(snapshot);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(output!))!);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(first.Capture()));
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleScenePresentation scene = new(assets, snapshot.Definition, true);
        string status = "pending";
        try
        {
            first.Tick();
            second.Tick();
            Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
            byte[] pixels = scene.Render(first, 320, 224).ToArray();
            Assert.Equal(pixels, new BattleScenePresentation(assets, snapshot.Definition, true).Render(second, 320, 224).ToArray());
            NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".after-tail", first.Capture(), pixels);
            status = "validated";
        }
        finally
        {
            Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
            File.WriteAllText(output! + ".migration.json", JsonSerializer.Serialize(new { Status = status, Source = Path.GetFullPath(source!), SourceSha256 = hash, Output = Path.GetFullPath(output!), Changes = new[] { new { Path = "/NativeEffectMemory/Textures/VramReadTail", Operation = "add", Bytes = NativeBattleEffectTextureState.PcPageReadTailLength, Value = "zero-initialized static read tail" } }, snapshot.Frame, NativeCallback = snapshot.NativeGraphics!.Callback }));
        }
    }
}
