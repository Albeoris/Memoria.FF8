using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleIfritEntryTests
{
    [Fact]
    public void Entry_reuses_shared_initialization_with_Ifrit_directories_and_actual201_pool()
    {
        var fixture = Fixture();
        NativeBattleIfritEntryStep result = Initialize(fixture);
        NativeBattleScriptedEffectMemory core = result.Memory.ScriptedEffects!;
        Assert.Equal(0x18748B4u, Dword(core, 0x2797690 + 0x98));
        Assert.Equal(0x187496Cu, Dword(core, 0x2797690 + 0x9C));
        Assert.Equal(0x1874A70u, Dword(core, 0x2797690 + 0xB0));
        Assert.Equal(fixture.Inputs.Resource0.Address, Dword(core, 0x2798A68));
        Assert.Equal(fixture.Inputs.Resource1.Address, Dword(core, 0x2798A6C));
        Assert.Equal(fixture.Inputs.TemporaryFrame.Address, Dword(core, 0x2797624));
        Assert.Equal(0xA5A5A5A5u, Dword(core, 0x2797690 + 0x78));
        Assert.Equal(0xA5A5A5A5u, Dword(core, 0x27975E8));
        NativeBattleEffectTaskPool pool = Assert.Single(result.Effect.Pools);
        Assert.Equal(0x2796E18u, result.Effect.ReturnedDescriptor);
        Assert.Equal(0x2796E28u, pool.BankAddress);
        Assert.Equal((ushort)16, pool.Stride);
        Assert.Equal(0xB25DF0u, Assert.Single(pool.Slots).Callback);
        Assert.Equal(0, pool.Head);
        Assert.Equal(fixture.Memory.Textures, result.Memory.Textures);
        Assert.Equal(fixture.Inputs.CurrentHeaderIndex, result.CurrentHeaderIndex);
        for (int index = 1; index < 8; index++)
        {
            int offset = fixture.Inputs.Resource1.ArenaOffset + 0x58 + index * 0x100;
            Assert.Equal(new byte[4], result.Memory.Arena!.Read(offset, 4));
            Assert.All(result.Memory.Arena.Read(offset + 4, 0xFC), value => Assert.Equal((byte)0xA5, value));
        }
        Assert.False(result.TemporaryFrame.Known[0x144]);
        Assert.True(result.TemporaryFrame.Known[0x145]);
    }

    [Fact]
    public void Independent_JSON_reproduces_entry_and_Water_wrapper_keeps_its_prior_validation()
    {
        var fixture = Fixture();
        NativeBattleIfritEntryStep expected = Initialize(fixture);
        NativeBattleIfritEntryStep actual = Initialize((RoundTrip(fixture.Memory), RoundTrip(fixture.Effect), RoundTrip(fixture.Directories), RoundTrip(fixture.Inputs)));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(RoundTrip(expected)));
        var water = NativeBattleWaterInitializationTests.Fixture();
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.Unknown();
        NativeBattleWaterInitializationResult legacy = NativeBattleWaterInitialization.InitializeA9B200(core, water.Arena, water.Profile, water.Inputs);
        NativeBattleWaterInitializationResult shared = NativeBattleWaterInitialization.InitializeScriptedEntry(core, water.Arena, new(water.Profile.InitializationDirectory, water.Profile.OwnerDirectory), water.Inputs, new(0x2796BA8, 0x2796BB8, 16, 1, 0xA9B870));
        Assert.Equal(JsonSerializer.Serialize(legacy), JsonSerializer.Serialize(shared));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterInitialization.InitializeA9B200(core, water.Arena, water.Profile with { Tables = new("tables", []) }, water.Inputs));
    }

    [Fact]
    public void Entry_rejects_missing_resource_core_and_consumed_bytes_without_hydrating_unknown_padding()
    {
        var fixture = Fixture();
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory with { ScriptedEffects = null }, fixture.Effect, fixture.Directories, fixture.Inputs)));
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory with { Arena = null }, fixture.Effect, fixture.Directories, fixture.Inputs)));
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory, fixture.Effect with { ResourcePointers = [] }, fixture.Directories, fixture.Inputs)));
        Assert.Throws<InvalidDataException>(() => Initialize((fixture.Memory, fixture.Effect, fixture.Directories with { OwnerDirectory = new("owners", []) }, fixture.Inputs)));
        Assert.Throws<InvalidDataException>(() => Initialize((fixture.Memory, fixture.Effect, fixture.Directories, fixture.Inputs with { InitializationDirectoryAddress = 0x186C84C })));
        Assert.Throws<InvalidDataException>(() => Initialize((fixture.Memory, fixture.Effect, fixture.Directories, fixture.Inputs with { Resource0 = fixture.Inputs.Resource0 with { ArenaOffset = 0 } })));
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory with { Arena = fixture.Memory.Arena!.MarkUnknown(fixture.Inputs.Resource0.ArenaOffset + 4, 4) }, fixture.Effect, fixture.Directories, fixture.Inputs)));
        NativeBattleIfritEntryStep unknown = Initialize((fixture.Memory with { ScriptedEffects = NativeBattleScriptedEffectMemory.Unknown() }, fixture.Effect, fixture.Directories, fixture.Inputs));
        Assert.Throws<NotSupportedException>(() => unknown.Memory.ScriptedEffects!.Read(0x2797690 + 0x78, 4));
        Assert.Throws<NotSupportedException>(() => unknown.Memory.ScriptedEffects!.Read(0x2797968 + 0x12, 2));
    }

    private static NativeBattleIfritEntryStep Initialize((NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleScriptedInitializationDirectories Directories, NativeBattleWaterInitializationInputs Inputs) fixture) => NativeBattleIfritEntry.InitializeB25780(fixture.Memory, fixture.Effect, fixture.Directories, fixture.Inputs);

    private static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleScriptedInitializationDirectories Directories, NativeBattleWaterInitializationInputs Inputs) Fixture()
    {
        var shared = NativeBattleWaterInitializationTests.Fixture();
        uint[] offsets = [0x20, 0xD8, 0x190, 0x1A8, 0x1C0, 0, 0x1DC];
        byte[] directory = new byte[28];
        for (int index = 0; index < offsets.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(index * 4), offsets[index]);
        }
        NativeBattleScriptedInitializationDirectories directories = new(new("initialization", directory.ToImmutableArray()), shared.Profile.OwnerDirectory);
        NativeBattleScriptedEffectMemory warm = new NativeBattleScriptedEffectMemory(NativeBattleEffectArena.Clear()).Write(NativeBattleScriptedEffectMemory.Address, Enumerable.Repeat((byte)0xA5, NativeBattleScriptedEffectMemory.Length).ToArray());
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = shared.Arena, ScriptedEffects = warm };
        NativeBattleWaterInitializationInputs inputs = shared.Inputs with { InitializationDirectoryAddress = 0x1874894 };
        NativeBattleOriginalEffectState effect = new(201, 200, new(201, 0xB25780, 0xB25750), new(inputs.Caster, 0, 0x14, inputs.CurrentHeaderIndex, 0, 0), null) { ResourcePointers = [new(0x2796E4C, inputs.Resource0.ArenaOffset), new(0x2796E48, inputs.Resource1.ArenaOffset)] };
        return (memory, effect, directories, inputs);
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private static uint Dword(NativeBattleScriptedEffectMemory core, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(core.Read(address, 4));
}
