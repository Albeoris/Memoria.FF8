using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedGraphicsProcessTests
{
    [Fact]
    public void Cold_graphics_owners_are_zero_separate_and_retained_across_resource_loads()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleScriptedGraphicsMemory graphics = Assert.IsType<NativeBattleScriptedGraphicsMemory>(memory.ScriptedGraphics);
        memory.Validate();
        Assert.Equal(new byte[0x2A8], graphics.Depths.Read(0, 0x2A8));
        Assert.Equal(new byte[32], graphics.ModuleViewBackup.ToArray());
        Assert.Equal((short)0, graphics.Dqa);
        Assert.Equal(0, graphics.Dqb);
        Assert.Equal(NativeBattleScriptedEffectMemory.Address + NativeBattleScriptedEffectMemory.Length, NativeBattleScriptedGraphicsMemory.DepthAddress);
        Assert.Same(graphics, memory.BeginLoad(222).ScriptedGraphics);
    }

    [Fact]
    public void Missing_imported_graphics_remains_missing_without_hydration()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedGraphics = null };
        string json = NativeBattleTestSnapshots.Json(memory);
        Assert.DoesNotContain("\"ScriptedGraphics\"", json);
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(json)!;
        restored.Validate();
        Assert.Null(restored.ScriptedGraphics);
        Assert.Null(restored.BeginLoad(222).ScriptedGraphics);
    }

    [Fact]
    public void Warm_depth_masks_backup_and_signed_controls_survive_JSON_and_next_resource_load()
    {
        NativeBattleScriptedGraphicsMemory graphics = new(NativeBattleEffectArena.Clear().Write(0, [1, 2, 3, 4]).MarkUnknown(2, 1), Enumerable.Range(0, 32).Select(index => (byte)(index + 1)).ToImmutableArray(), -321, int.MinValue + 123);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedGraphics = graphics };
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        restored.Validate();
        Assert.Equal(NativeBattleTestSnapshots.Json(graphics), NativeBattleTestSnapshots.Json(restored.ScriptedGraphics));
        NativeBattleEffectMemory loaded = restored.BeginLoad(222);
        Assert.Same(restored.ScriptedGraphics, loaded.ScriptedGraphics);
        NativeBattleScriptedGraphicsMemory retained = Assert.IsType<NativeBattleScriptedGraphicsMemory>(loaded.ScriptedGraphics);
        Assert.Throws<NotSupportedException>(() => retained.Depths.Read(2, 1));
        Assert.Equal(new byte[] { 1, 2 }, retained.Depths.Read(0, 2));
        NativeBattleScriptedGraphicsMemory expected = graphics with { Depths = graphics.Depths.Write(2, [9]), Dqb = 77 };
        NativeBattleScriptedGraphicsMemory actual = retained with { Depths = retained.Depths.Write(2, [9]), Dqb = 77 };
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
    }

    [Fact]
    public void Explicit_graphics_owner_requires_all_controls_and_rejects_out_of_window_storage()
    {
        NativeBattleScriptedGraphicsMemory graphics = NativeBattleScriptedGraphicsMemory.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => (graphics with { Depths = graphics.Depths.Write(NativeBattleScriptedGraphicsMemory.DepthLength, [1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (graphics with { Depths = graphics.Depths.MarkUnknown(NativeBattleScriptedGraphicsMemory.DepthLength, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (graphics with { ModuleViewBackup = [] }).Validate());
        foreach (string property in new[] { "Dqa", "Dqb", "Depths", "ModuleViewBackup" })
        {
            JsonObject json = JsonNode.Parse(NativeBattleTestSnapshots.Json(graphics))!.AsObject();
            json.Remove(property);
            Assert.Throws<JsonException>(() => NativeBattleTestSnapshots.RestoreJson<NativeBattleScriptedGraphicsMemory>(json.ToJsonString()));
        }
    }
}
