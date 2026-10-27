using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect312Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect312StateTests
{
    [Fact]
    public void Cold_sparse_state_excludes_pool_descriptors_and_unobserved_gaps()
    {
        NativeBattleEffect312State state = NativeBattleEffect312State.FromColdProcess();
        state.Validate();
        Assert.True(state.ColdSourceOrigin);
        Assert.False(state.StaticPacketBound);
        Assert.Null(state.CapturePacket);
        Assert.NotNull(state.RootBank);
        Assert.NotNull(state.ChildBank);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Throws<InvalidDataException>(() => state.Read(0x223CCA4, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x223CCD8, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x223E1A8, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2240EC4, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2240EE0, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(CapturePacketAddress, CapturePacketLength));
    }

    [Fact]
    public void Marker_and_matrix_writes_preserve_unknown_payload_padding_and_anchor_W()
    {
        NativeBattleEffect312State state = NativeBattleEffect312State.FromUnknown().WriteDword(0x223CCE8, 0).WriteDword(0x223D648, 0).WriteWord(0x223E148, 4096).WriteWord(0x2240EC8, 123);
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect312State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Equal(0u, state.ReadDword(0x223CCE8));
        Assert.Equal(0u, state.ReadDword(0x223D648));
        Assert.Equal((ushort)4096, state.ReadWord(0x223E148));
        Assert.Equal((ushort)123, state.ReadWord(0x2240EC8));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(0x223CCEC));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(0x223D64C));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(0x223E15A));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(0x2240ECE));
    }

    [Fact]
    public void Task_banks_transfer_exclusively_with_retained_warm_payload_and_knownness()
    {
        NativeBattleEffect312State state = NativeBattleEffect312State.FromUnknown();
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleEffectArena bank = state.ReadBank(index).Write(12, BitConverter.GetBytes((ushort)0xCAFE));
            state = state.DetachBank(index);
            Assert.Throws<InvalidDataException>(() => state.ReadBank(index));
            state = state.AttachBank(Pools[index].Bank, bank);
            Assert.Throws<InvalidDataException>(() => state.AttachBank(index, bank));
        }
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect312State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        for (int index = 0; index < Pools.Length; index++)
        {
            Assert.Equal(BitConverter.GetBytes((ushort)0xCAFE), state.ReadBank(index).Read(12, 2));
            Assert.Throws<NotSupportedException>(() => state.ReadBank(index).Read(14, 2));
        }
    }

    [Fact]
    public void Capture_seed_binds_once_and_retains_current_links_across_JSON()
    {
        NativeBattleEffect312Resources resources = new(null!, null!, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(CapturePacketAddress, new byte[CapturePacketLength].ToImmutableArray()));
        NativeBattleEffect312State state = NativeBattleEffect312State.FromColdProcess().BindCapturePacket(resources);
        state = state.WriteCapturePacket(state.ReadCapturePacket().Write(0, BitConverter.GetBytes(0x05123456u)));
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect312State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Same(state, state.BindCapturePacket(null!));
        Assert.Equal(BitConverter.GetBytes(0x05123456u), state.ReadCapturePacket().Read(0, 4));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect312State.FromUnknown().BindCapturePacket(resources));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect312State.FromColdProcess().ReadCapturePacket());
        Assert.Throws<InvalidDataException>(() => (state with { CapturePacket24 = null }).Validate());
        Assert.Throws<InvalidDataException>(() => state.WriteCapturePacket(NativeBattleEffectArena.Clear().Write(CapturePacketLength, new byte[1])));
    }
}
