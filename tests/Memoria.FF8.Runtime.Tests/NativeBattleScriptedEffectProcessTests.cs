using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedEffectProcessTests
{
    private const uint Base = NativeBattleScriptedEffectMemory.Address;

    [Fact]
    public void Genuine_cold_factory_establishes_one_known_zero_shared_process_owner()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory.Validate();
        NativeBattleScriptedEffectMemory core = Assert.IsType<NativeBattleScriptedEffectMemory>(memory.ScriptedEffects);
        Assert.Equal(new byte[NativeBattleScriptedEffectMemory.Length], core.Read(Base, NativeBattleScriptedEffectMemory.Length));
        NativeBattleEffectMemory loaded = memory.BeginLoad(221);
        Assert.Same(core, loaded.ScriptedEffects);
        Assert.Equal(1, loaded.Active);
        Assert.Equal(221, loaded.SelectedIndex);
        Assert.Equal(new byte[0x17C], loaded.ScriptedEffects!.ReadHeader().Read(0, 0x17C));
    }

    [Fact]
    public void Imported_missing_process_property_remains_missing_through_validation_load_and_JSON()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedEffects = null };
        memory.Validate();
        string json = JsonSerializer.Serialize(memory);
        Assert.DoesNotContain("\"ScriptedEffects\"", json);
        NativeBattleEffectMemory imported = JsonSerializer.Deserialize<NativeBattleEffectMemory>(json)!;
        imported.Validate();
        Assert.Null(imported.ScriptedEffects);
        NativeBattleEffectMemory loaded = imported.BeginLoad(221);
        loaded.Validate();
        Assert.Null(loaded.ScriptedEffects);
        string loadedJson = JsonSerializer.Serialize(loaded);
        Assert.DoesNotContain("\"ScriptedEffects\"", loadedJson);
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectMemory>(loadedJson)!.ScriptedEffects);
    }

    [Fact]
    public void Warm_raw_bytes_and_unknown_padding_survive_resource_loads_and_JSON_without_owner_replacement()
    {
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess().Write(Base + 0x80, [0x11, 0x22, 0xAC, 0xBD]).MarkUnknown(Base + 0x82, 2).Write(Base + 0x1BAF, [0xEE]);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(100);
        memory = memory with { ScriptedEffects = core, Arena = memory.Arena!.Write(0, [0x76, 0x87]) };
        NativeBattleEffectMemory loaded = memory.BeginLoad(221);
        Assert.Same(core, loaded.ScriptedEffects);
        Assert.Equal(new byte[] { 0, 0 }, loaded.Arena!.Read(0, 2));
        Assert.Equal(new byte[] { 0x11, 0x22 }, loaded.ScriptedEffects!.Read(Base + 0x80, 2));
        Assert.Equal(new byte[] { 0xEE }, loaded.ScriptedEffects!.Read(Base + 0x1BAF, 1));
        Assert.Throws<NotSupportedException>(() => loaded.ScriptedEffects!.Read(Base + 0x82, 2));
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(loaded))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(loaded.ScriptedEffects), JsonSerializer.Serialize(restored.ScriptedEffects));
        Assert.Equal(new byte[] { 0xAC, 0xBD }, restored.ScriptedEffects!.ReadHeader().Bytes.Skip(0x82).Take(2));
        Assert.False(restored.ScriptedEffects!.ReadHeader().Known[0x82]);
        NativeBattleEffectMemory nextLoad = restored.BeginLoad(99);
        Assert.Same(restored.ScriptedEffects, nextLoad.ScriptedEffects);
        Assert.Equal(JsonSerializer.Serialize(restored.ScriptedEffects), JsonSerializer.Serialize(nextLoad.ScriptedEffects));
    }

    [Fact]
    public void Explicit_unknown_owner_keeps_only_actual_partial_writes_known_after_load_and_restore()
    {
        NativeBattleScriptedEffectMemory unknown = NativeBattleScriptedEffectMemory.Unknown().Write(Base + 0x44, [3]).Write(Base + 0x120, [0x55, 0x66]);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedEffects = unknown };
        memory.Validate();
        NativeBattleEffectMemory loaded = memory.BeginLoad(221);
        Assert.Same(unknown, loaded.ScriptedEffects);
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(loaded))!;
        NativeBattleScriptedEffectMemory actual = restored.ScriptedEffects!;
        Assert.Equal(new byte[] { 3 }, actual.Read(Base + 0x44, 1));
        Assert.Equal(new byte[] { 0x55, 0x66 }, actual.Read(Base + 0x120, 2));
        Assert.Throws<NotSupportedException>(() => actual.Read(Base + 0x43, 1));
        Assert.Throws<NotSupportedException>(() => actual.Read(Base + 0x45, 1));
        Assert.Throws<NotSupportedException>(() => actual.Read(Base + 0x122, 1));
        Assert.Throws<NotSupportedException>(() => actual.Read(Base + 0x1BAF, 1));
        NativeBattleEffectMemory malformed = memory with { ScriptedEffects = new(NativeBattleEffectArena.Clear().Write(NativeBattleScriptedEffectMemory.Length, [1])) };
        Assert.Throws<InvalidDataException>(() => malformed.Validate());
    }
}
