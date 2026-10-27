using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect80StateTests
{
    [Fact]
    public void Cold_origin_exposes_only_exact_BSS_and_nine_exclusive_inactive_banks()
    {
        NativeBattleEffect80State state = NativeBattleEffect80State.FromColdProcess();
        state.Validate();
        Assert.True(state.ColdSourceOrigin);
        Assert.Empty(state.NodeBanks);
        Assert.Empty(state.InitializedSources);
        Assert.Equal(9, state.FixedBanks.Count);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Throws<InvalidDataException>(() => state.Read(0x25D4942, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(Pools[0].Descriptor, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(Pools[0].Bank, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(0x25DF00A, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(0x25DF311, 1));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(TrailPointerAddress));
        NativeBattleEffectArena bank = state.ReadBank(5).Write(0x1F3E, BitConverter.GetBytes((ushort)0xCAFE));
        state = state.DetachBank(5);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(5));
        state = state.AttachBank(5, bank);
        Assert.Equal(new byte[] { 0xFE, 0xCA }, state.ReadBank(5).Read(0x1F3E, 2));
        Assert.Throws<InvalidDataException>(() => state.AttachBank(5, bank));
    }

    [Fact]
    public void Explicit_original_binding_is_once_and_unknown_import_cannot_rehydrate()
    {
        NativeBattleEffect80Resources resources = Sources();
        NativeBattleEffect80State unknown = NativeBattleEffect80State.FromUnknown();
        Assert.Throws<NotSupportedException>(() => unknown.BindNodeSource(resources, 0x1749C64));
        Assert.Throws<NotSupportedException>(() => unknown.BindInitializedSource(resources, TrailAddress));
        NativeBattleEffect80State state = NativeBattleEffect80State.FromColdProcess().BindNodeSource(resources, 0x1749C64).BindInitializedSource(resources, TrailPointerAddress).BindInitializedSource(resources, TrailAddress);
        Assert.Single(state.NodeBanks);
        Assert.Equal(TrailAddress, state.ReadDword(TrailPointerAddress));
        Assert.Contains(state.Read(TrailAddress, TrailLength), value => value != 0);
        state = state.WriteDword(0x1749C64, 1).WriteWord(TrailAddress + 6, ushort.MaxValue).WriteDword(TrailPointerAddress, TrailAddress + 24);
        NativeBattleEffect80State again = state.BindNodeSource(resources, 0x1749C64).BindInitializedSource(resources, TrailAddress).BindInitializedSource(resources, TrailPointerAddress);
        Assert.Equal(1u, again.ReadDword(0x1749C64));
        Assert.Equal((ushort)65535, again.ReadWord(TrailAddress + 6));
        Assert.Equal(TrailAddress + 24, again.ReadDword(TrailPointerAddress));
        Assert.Equal(state, again);
        NativeBattleEffect80State retainedUnknown = state with { InitializedSources = state.InitializedSources.SetItem(TrailPointerAddress, NativeBattleEffectArena.Clear().MarkUnknown(0, 4)) };
        retainedUnknown = retainedUnknown.BindInitializedSource(resources, TrailPointerAddress);
        Assert.Throws<NotSupportedException>(() => retainedUnknown.ReadDword(TrailPointerAddress));
    }

    [Fact]
    public void Word_aliases_and_partial_rows_preserve_actual_byte_knownness()
    {
        NativeBattleEffect80State state = NativeBattleEffect80State.FromUnknown().WriteWord(0x25DF31C, 10).WriteWord(0x25DF31E, 16);
        Assert.Equal(0x0010000Au, state.ReadDword(0x25DF31C));
        NativeBattleEffectArena row = state.ReadView(RowsAddress, 16).Write(0, new byte[6]).Write(8, BitConverter.GetBytes(0x81234567u)).Write(12, BitConverter.GetBytes(int.MinValue));
        state = state.WriteView(RowsAddress, row, 16);
        Assert.Throws<NotSupportedException>(() => state.ReadWord(RowsAddress + 6));
        Assert.Equal(0x81234567u, state.ReadDword(RowsAddress + 8));
        Assert.Equal(0x80000000u, state.ReadDword(RowsAddress + 12));
        state.Validate();
    }

    [Fact]
    public void Json_retains_selected_node_data_trail_partial_pointer_and_detached_bank()
    {
        NativeBattleEffect80State state = NativeBattleEffect80State.FromColdProcess().BindNodeSource(Sources(), 0x1750660).BindInitializedSource(Sources(), TrailAddress).DetachBank(0).WriteWord(TrailAddress + 4, 0xBEEF);
        state = state with { InitializedSources = state.InitializedSources.Add(TrailPointerAddress, NativeBattleEffectArena.Clear().MarkUnknown(0, 4).Write(0, BitConverter.GetBytes((ushort)0xAC20))) };
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state));
        NativeBattleEffect80State restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect80State>(json)!);
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        Assert.Null(restored.FixedBanks[Pools[0].Bank]);
        Assert.Single(restored.NodeBanks);
        Assert.Equal((ushort)0xBEEF, restored.ReadWord(TrailAddress + 4));
        Assert.Equal((ushort)0xAC20, restored.ReadWord(TrailPointerAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(TrailPointerAddress + 2));
    }

    private static NativeBattleEffect80Resources Sources() => new(new("mag079.tim", []), new("camera", []), new("vibration", []), NodeSourceLengths.ToImmutableDictionary(pair => pair.Key, pair => new NativeBattleEffect80NodeResourceBank(pair.Key, new byte[pair.Value].ToImmutableArray())), InitializedSourceLengths.ToImmutableDictionary(pair => pair.Key, pair => pair.Key == TrailPointerAddress ? BitConverter.GetBytes(TrailAddress).ToImmutableArray() : Enumerable.Repeat((byte)0xA5, pair.Value).ToImmutableArray()), [], ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, ImmutableDictionary<uint, uint>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, TargetAddress);
}
