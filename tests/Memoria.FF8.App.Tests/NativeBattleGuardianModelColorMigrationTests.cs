using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianModelColorMigrationTests
{
    [Fact]
    public void MigrateOnlyNamed30ProvedInactivePendingActorsThenRunTheActualModelCallback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_MODEL_COLOR_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_MODEL_COLOR_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "The exact A3-migrated30 and a distinct output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] originalBytes = File.ReadAllBytes(source!);
        const string originalHash = "E247076B3B7A213051FCD5AF9709AD228EC3B28ACBDCAE363F35E9D1170E3B00";
        Assert.Equal(originalHash, Convert.ToHexString(SHA256.HashData(originalBytes)));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(originalBytes)!;
        Assert.Equal(255L, original.Frame);
        Assert.Equal(496L, original.NativeGraphics!.Callback);
        Assert.Equal((ushort)116, original.NativeGraphics.OriginalEffect!.Binding.Effect);
        Assert.Null(original.NativeEffectMemory!.ModelColorOverrides);
        Assert.Null(original.Definition.NativeRetainedMemory!.EffectMemory!.ModelColorOverrides);
        NativeBattleGraphicsModel visible = original.NativeGraphics.Models[3];
        Assert.Equal((byte)3, visible.Index);
        Assert.Equal(0, visible.Flags0 & 0x808);
        Assert.Equal(0, visible.Flags2!.Value & 4);
        Assert.True((visible.Flags8!.Value & 0x2160510) == 0);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        NativeBattleModelColorOverrides cold = game.ReadColdProcessNativeBattleMemory().EffectMemory!.ModelColorOverrides!;
        Assert.Equal((byte?)255, cold.Slots[0].Actor);
        Assert.Equal((byte?)0, cold.Slots[1].Actor);
        NativeBattleModelColorOverrides proved = new([new(255, null), new(255, null)]);
        JsonNode originalNode = JsonNode.Parse(originalBytes)!;
        JsonNode migratedNode = originalNode.DeepClone();
        JsonObject memory = migratedNode["NativeEffectMemory"]!.AsObject();
        Assert.False(memory.ContainsKey("ModelColorOverrides"));
        memory["ModelColorOverrides"] = JsonSerializer.SerializeToNode(proved);
        JsonNode reverted = migratedNode.DeepClone();
        Assert.True(reverted["NativeEffectMemory"]!.AsObject().Remove("ModelColorOverrides"));
        Assert.True(JsonNode.DeepEquals(originalNode, reverted));
        BattleSnapshot migrated = migratedNode.Deserialize<BattleSnapshot>()!;
        Assert.Equal(JsonSerializer.Serialize(original.Definition), JsonSerializer.Serialize(migrated.Definition));
        BattleSession first = BattleSession.Restore(migrated);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback);
        second.AdvanceNativeGraphicsPresentation(callback);
        BattleSnapshot after = first.Capture();
        Assert.Equal(255L, after.Frame);
        Assert.Equal(497L, after.NativeGraphics!.Callback);
        Assert.Equal(original.NativeStage!.ModelColors!.Color40, after.NativeGraphics.Models[3].Color40);
        Assert.True(after.NativeGraphics.Models[3].Transform!.Vertical34 == 0);
        Assert.All(after.NativeEffectMemory!.ModelColorOverrides!.Slots, slot =>
        {
            Assert.Equal((byte?)255, slot.Actor);
            Assert.Null(slot.Color);
        });
        Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(second.Capture()));
        Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(BattleSession.Restore(after).Capture()));
        using BattleAssets assets = new(game.GameDirectory);
        BattleScenePresentation scene = new(assets, migrated.Definition, true);
        Assert.Equal(scene.Render(first, 320, 224).ToArray(), scene.Render(second, 320, 224).ToArray());
        for (int index = 0; index < 128 && first.NativeEffectMemory!.Active != 0; index++)
        {
            first.AdvanceNativeGraphicsPresentation(callback);
            second.AdvanceNativeGraphicsPresentation(callback);
        }
        BattleSnapshot completed = first.Capture();
        Assert.Equal(0, completed.NativeEffectMemory!.Active);
        Assert.Equal(0u, completed.NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        Assert.DoesNotContain(completed.NativeGraphics.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Action104);
        NativeBattleGraphicsModel caster = completed.NativeGraphics.Models[0];
        Assert.True(caster.Phase6 == 16);
        Assert.Equal(0, caster.Flags0 & 0x808);
        Assert.Equal(0, caster.Flags2!.Value & 4);
        Assert.True((caster.Flags8!.Value & 0x2160510) == 0);
        Assert.Equal(completed.NativeStage!.ModelColors!.Color40, caster.Color40);
        Assert.NotEqual(0u, caster.Color40);
        Assert.Equal(JsonSerializer.Serialize(completed), JsonSerializer.Serialize(second.Capture()));
        Assert.Equal(scene.Render(first, 320, 224).ToArray(), scene.Render(second, 320, 224).ToArray());
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, migratedNode);
        }
        Assert.Equal(originalHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        Console.WriteLine($"Exact A3-migrated30 pending-color output={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; only live ModelColorOverrides added with Actor255/unknownRGB in both rows, Definition unchanged. Proof: first declared cold actor0 admission consumes second row,1732-function bounded entry/current116 closure excludes both indexed writers. Actual callback496→497 validates native50A0A0 stageRGB/Vertical34; remaining current cast completes at{completed.NativeGraphics.Callback} with casterRGB{caster.Color40:X6}, equal to retained stage color. Independent whole states/JSON/copied render agree; unused pending RGB remains unknown.");
    }
}
