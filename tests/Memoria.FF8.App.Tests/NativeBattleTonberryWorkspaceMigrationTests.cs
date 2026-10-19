using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleTonberryWorkspaceMigrationTests
{
    [Fact]
    public void Mark_only_unpublished_static_projection_unknown_then_continue_with_live_TIM_intact()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_TONBERRY_WORKSPACE_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TONBERRY_WORKSPACE_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "Select the standalone Tonberry workspace boundary and a distinct migrated output.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] bytes = File.ReadAllBytes(source!);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        JsonNode original = JsonNode.Parse(bytes)!;
        BattleSnapshot before = original.Deserialize<BattleSnapshot>()!;
        Assert.Equal(227L, before.Frame);
        Assert.Equal(307L, before.NativeGraphics!.Callback);
        NativeBattleTonberry90Resources resources = before.NativeGraphics.OriginalEffect!.Binding.Tonberry90Resources!;
        NativeBattleTonberry90State shared = before.NativeEffectMemory!.Tonberry90!;
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(before.NativeGraphics.OriginalEffect.Pools).Read(shared.CreatureModelState!.TaskAddress);
        int workBytes = checked(resources.CreatureModel!.Geometry.Where((_, index) => (task.ReadDword(0xAC) & (1u << (index & 31))) != 0).Max(mesh => mesh.Vertices.Length) * 8);
        Assert.Equal(3936, workBytes);
        uint workAddress = NativeBattleTonberry90Creature.ProjectedAddress767610;
        int workOffset = checked((int)(workAddress - NativeBattleTonberry90State.GlobalAddress));
        byte[] tim = resources.Files.Single(file => file.FileId == 416).Data.ToArray();
        Assert.Equal(7776, tim.Length);
        Assert.Equal(tim, before.NativeEffectMemory.Arena!.Read(0xC000, tim.Length));
        Assert.Equal(0, before.NativeEffectMemory.Textures!.Count);
        NativeBattleEffectArena globals = shared.Globals.MarkUnknown(workOffset, workBytes);
        Assert.Equal(JsonSerializer.Serialize(shared.Globals.Regions), JsonSerializer.Serialize(globals.Regions));
        JsonNode migrated = original.DeepClone();
        migrated["NativeEffectMemory"]!["Tonberry90"]!["Globals"]!["UnknownRegions"] = JsonSerializer.SerializeToNode(globals.UnknownRegions);
        JsonNode reverted = migrated.DeepClone();
        reverted["NativeEffectMemory"]!["Tonberry90"]!["Globals"]!["UnknownRegions"] = original["NativeEffectMemory"]!["Tonberry90"]!["Globals"]!["UnknownRegions"]!.DeepClone();
        Assert.True(JsonNode.DeepEquals(original, reverted));
        BattleSnapshot snapshot = migrated.Deserialize<BattleSnapshot>()!;
        BattleSession first = BattleSession.Restore(snapshot);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(first.Capture()));
        Assert.Throws<NotSupportedException>(() => first.NativeEffectMemory!.Tonberry90!.Read(workAddress, 1));
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleScenePresentation scene = new(assets, snapshot.Definition, true);
        Assert.Equal(scene.Render(BattleSession.Restore(before), 320, 224).ToArray(), new BattleScenePresentation(assets, snapshot.Definition, true).Render(first, 320, 224).ToArray());
        string directory = Path.GetDirectoryName(Path.GetFullPath(output!))!;
        Directory.CreateDirectory(directory);
        try
        {
            first.Tick();
            second.Tick();
            Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
            Assert.Equal(308L, first.NativeGraphics!.Callback);
            Assert.Equal(tim, first.NativeEffectMemory!.Arena!.Read(0xC000, tim.Length));
            Assert.Equal(workBytes, first.NativeEffectMemory.Tonberry90!.Read(workAddress, workBytes).Length);
            Assert.Equal(0, first.NativeEffectMemory.Textures!.Count);
            byte[] pixels = scene.Render(first, 320, 224).ToArray();
            Assert.Equal(pixels, new BattleScenePresentation(assets, snapshot.Definition, true).Render(second, 320, 224).ToArray());
            using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, migrated);
            }
            NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".after-workspace", first.Capture(), pixels);
            Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(output!))!).Capture()));
        }
        catch
        {
            NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".before-boundary", snapshot, null);
            NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".failed-live", first.Capture(), null);
            throw;
        }
    }
}
