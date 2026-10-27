using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect301Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect301StateTests
{
    [Fact]
    public void Whole_process_cold_factory_keeps_exact_BSS_and_an_unbound_script_input()
    {
        NativeBattleEffect301State state = Assert.IsType<NativeBattleEffect301State>(NativeBattleEffectMemory.FromColdProcess().Effect301);
        state.Validate();
        Assert.Equal(3, OwnedRanges.Length);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[20], state.ReadBank(0).Read(0, 20));
        Assert.True(state.ScriptSourcePending);
        Assert.Throws<NotSupportedException>(() => state.ReadDword(NativeBattleEffect301State.ScriptPointerAddress));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2507750, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x25076E4, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2507728, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, 4));
    }

    [Fact]
    public void Only_genuine_cold_unobserved_cursor_can_bind_the_original_four_byte_input_once()
    {
        NativeBattleEffect301Resources resources = Synthetic(0x12345678);
        NativeBattleEffect301State state = NativeBattleEffect301State.FromColdProcess().BindSources(resources);
        Assert.False(state.ScriptSourcePending);
        Assert.Equal(0x12345678u, state.ReadDword(NativeBattleEffect301State.ScriptPointerAddress));
        state = state.WriteDword(NativeBattleEffect301State.ScriptPointerAddress, 0x12345890);
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect301State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        Assert.Equal(0x12345890u, state.BindSources(resources).ReadDword(NativeBattleEffect301State.ScriptPointerAddress));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect301State.FromUnknown().BindSources(resources));
        NativeBattleEffect301State observed = NativeBattleEffect301State.FromColdProcess().Write(NativeBattleEffect301State.ScriptPointerAddress, new byte[] { 42 });
        Assert.Throws<InvalidDataException>(() => observed.BindSources(resources));
    }

    [Fact]
    public void Bound_cursor_partial_unknownness_survives_JSON_and_is_never_refilled()
    {
        NativeBattleEffect301Resources resources = Synthetic(0x12345678);
        NativeBattleEffect301State state = NativeBattleEffect301State.FromColdProcess().BindSources(resources);
        state = state.WriteView(NativeBattleEffect301State.ScriptPointerAddress + 1, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2);
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect301State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        state = state.BindSources(resources);
        Assert.Equal((byte)0x78, state.Read(NativeBattleEffect301State.ScriptPointerAddress, 1)[0]);
        Assert.Equal((byte)0x12, state.Read(NativeBattleEffect301State.ScriptPointerAddress + 3, 1)[0]);
        Assert.Throws<NotSupportedException>(() => state.ReadDword(NativeBattleEffect301State.ScriptPointerAddress));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(NativeBattleEffect301State.ScriptPointerAddress + 1));
    }

    [Fact]
    public void Inactive_root_transfers_exclusively_and_BSS_widths_preserve_unknown_neighbors()
    {
        NativeBattleEffect301State state = NativeBattleEffect301State.FromUnknown().WriteWord(0x2507744, 4);
        NativeBattleEffectArena bank = state.ReadBank(0).Write(18, BitConverter.GetBytes((ushort)0xBEEF));
        state = state.DetachBank(0);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = state.AttachBank(0, bank);
        Assert.Throws<InvalidDataException>(() => state.AttachBank(0, bank));
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect301State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Equal((ushort)4, state.ReadWord(0x2507744));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(0x2507744));
        Assert.Equal(BitConverter.GetBytes((ushort)0xBEEF), state.ReadBank(0).Read(18, 2));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(0).Read(0, 4));
        Assert.Throws<InvalidDataException>(() => state.ReadBank(1));
    }

    private static NativeBattleEffect301Resources Synthetic(uint pointer) => new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, null!, BitConverter.GetBytes(pointer).ToImmutableArray(), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
}
