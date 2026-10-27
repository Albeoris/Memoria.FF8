using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleNodeInitializationTests
{
    [Fact]
    public void Curaga_fixups_remain_private_through_observers_and_unpublished_suffix_rolls_back()
    {
        ImmutableArray<byte> bytes = new byte[NativeBattleCuragaExecutableState.Length].ToImmutableArray();
        NativeBattleCuragaExecutableState state = new(0, bytes);
        NativeBattleCuragaExecutableState history = NativeBattleStorageSnapshots.Capture(state);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(28) with { CuragaExecutable = state };
        using (NativeBattleTaskOperation operation = new(memory, []))
        {
            using NativeBattleNodeInitialization<NativeBattleCuragaExecutableState> initialization = state.Prepare87EA20(new(bytes));
            Assert.Equal(0u, state.Data!.ReadUInt32(0));
            NativeBattleTaskOperation.AcceptPublishedMemory(memory);
            NativeBattleCuragaExecutableState initialized = initialization.Publish();
            Assert.Same(state.Data, initialized.Data);
            Assert.Equal(1u, initialized.Data!.ReadUInt32(0));
        }
        state.Validate();
        Assert.Equal(0u, state.Data!.ReadUInt32(0));
        Assert.Equal(0u, history.Data!.ReadUInt32(0));
    }

    [Fact]
    public void Accepted_curaga_initialization_survives_failure_and_roundtrips_the_legacy_data_array()
    {
        ImmutableArray<byte> bytes = new byte[NativeBattleCuragaExecutableState.Length].ToImmutableArray();
        NativeBattleCuragaExecutableState state = new(0, bytes);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(28) with { CuragaExecutable = state };
        NativeBattleCuragaExecutableState initialized;
        using (NativeBattleTaskOperation operation = new(memory, []))
        {
            using NativeBattleNodeInitialization<NativeBattleCuragaExecutableState> initialization = state.Prepare87EA20(new(bytes));
            initialized = initialization.Publish();
            NativeBattleTaskOperation.AcceptPublishedMemory(memory with { CuragaExecutable = initialized });
            initialized.Data!.Arena.WriteUInt32(4, 99);
        }
        initialized.Validate();
        Assert.Equal(1u, initialized.Data!.ReadUInt32(0));
        Assert.Equal(0u, initialized.Data.ReadUInt32(4));
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(initialized));
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("Data").ValueKind);
        NativeBattleCuragaExecutableState restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleCuragaExecutableState>(json)!);
        restored.Data!.Arena.WriteUInt32(4, 7);
        Assert.Equal(0u, initialized.Data.ReadUInt32(4));
        Assert.Equal(7u, restored.Data.ReadUInt32(4));
    }
}
