using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect207Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect207StateTests
{
    [Fact]
    public void Cold_factory_retains_exact_copied_matrix_and_inactive_pool_owners()
    {
        NativeBattleEffect207State state = Assert.IsType<NativeBattleEffect207State>(NativeBattleEffectMemory.FromColdProcess().Effect207);
        state.Validate();
        Assert.Equal(6, OwnedRanges.Length);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Single(state.FixedBanks);
        Assert.Equal(2, state.CommonQueueStorage.Count);
        Assert.Throws<InvalidDataException>(() => state.Read(ParticleDescriptor + 8, 8));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(ViewAddress + 32, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, 4));
    }

    [Fact]
    public void Partial_matrix_writes_preserve_unknown_offdiagonals_and_padding_through_JSON()
    {
        NativeBattleEffect207State state = NativeBattleEffect207State.FromUnknown().WriteWord(MatrixAddress, 320).WriteWord(MatrixAddress + 8, 256).WriteDword(MatrixAddress + 20, 512).WriteDword(MatrixAddress + 24, 384);
        state = JsonSerializer.Deserialize<NativeBattleEffect207State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Equal((ushort)320, state.ReadWord(MatrixAddress));
        Assert.Equal(512u, state.ReadDword(MatrixAddress + 20));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(MatrixAddress + 2));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(MatrixAddress + 18));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(MatrixAddress + 28));
    }

    [Fact]
    public void Each_COMMON_queue_transfers_exclusively_and_preserves_retained_orphan_links()
    {
        NativeBattleEffect207State state = NativeBattleEffect207State.FromUnknown();
        foreach (NativeBattleCureFamilyPool shape in CommonPools)
        {
            NativeBattleEffectArena queue = state.ReadCommonQueue(shape.Descriptor).Write(0, BitConverter.GetBytes(0xCAFEBABEu));
            state = state.DetachCommonQueue(shape.Descriptor);
            Assert.Throws<InvalidDataException>(() => state.ReadDword(shape.Descriptor));
            state = state.AttachCommonQueue(shape.Descriptor, queue);
            Assert.Throws<InvalidDataException>(() => state.AttachCommonQueue(shape.Descriptor, queue));
        }
        state = JsonSerializer.Deserialize<NativeBattleEffect207State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        foreach (NativeBattleCureFamilyPool shape in CommonPools)
        {
            Assert.Equal(0xCAFEBABEu, state.ReadDword(shape.Descriptor));
            Assert.Throws<NotSupportedException>(() => state.ReadDword(shape.Descriptor + 4));
        }
    }

    [Fact]
    public void Current_copied_bytes_and_root_bank_retain_mutations_without_refill()
    {
        NativeBattleEffect207State state = NativeBattleEffect207State.FromUnknown().WriteWord(MutableMeshAddress + 14, 0xBEEF);
        NativeBattleEffectArena bank = state.ReadBank(0).Write(14, BitConverter.GetBytes((ushort)0xCAFE));
        state = state.DetachBank(0);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = state.AttachBank(0, bank);
        state = JsonSerializer.Deserialize<NativeBattleEffect207State>(JsonSerializer.Serialize(state))!;
        Assert.Equal((ushort)0xBEEF, state.ReadWord(MutableMeshAddress + 14));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(MutableMeshAddress));
        Assert.Equal(BitConverter.GetBytes((ushort)0xCAFE), state.ReadBank(0).Read(14, 2));
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleEffectMemory.FromColdProcess() with { Effect207 = null }))!.Effect207);
    }
}
