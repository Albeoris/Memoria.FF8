using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleStare45Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStare45StateTests
{
    [Fact]
    public void Cold_process_owns_exact_pointed_object_scratch_and_basis_without_PE_clones()
    {
        var state = NativeBattleStare45State.FromColdProcess();
        state.Validate();
        Assert.Equal(new byte[EffectObjectLength], state.Read(EffectObjectAddress, EffectObjectLength));
        Assert.Equal(new byte[ScratchLength], state.Read(ScratchAddress, ScratchLength));
        Assert.Equal(new byte[BasisLength], state.Read(BasisAddress, BasisLength));
        Assert.Equal(0u, state.ReadDword(InitializerGlobal));
        Assert.Equal(6, state.InactiveBanks.Length);
        Assert.Throws<InvalidDataException>(() => state.ReadWord(EffectObjectPointer));
        Assert.Throws<InvalidDataException>(() => state.ReadWord(ProgrammeAAddress));
    }

    [Fact]
    public void Sparse_ranges_preserve_unowned_gaps_and_shared_COMMON_camera_owners()
    {
        var state = NativeBattleStare45State.FromColdProcess();
        foreach (uint address in new[] { EffectObjectAddress + EffectObjectLength, ScratchAddress + ScratchLength, BasisAddress + BasisLength, SharedViewAddress, 0x1D8E054u, 0x20DFAB8u })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.Write(checked((int)(ScratchAddress + ScratchLength - BaseAddress)), new byte[1]) }).Validate());
    }

    [Fact]
    public void Partial_XYZ_morph_and_basis_RT_writes_retain_W_padding_and_unknown_masks_through_JSON()
    {
        var state = NativeBattleStare45State.FromUnknown();
        state.Validate();
        state = state.WriteWord(ScratchAddress + 6, 0x1234).Write(ScratchAddress, new byte[] { 1, 2, 3, 4, 5, 6 });
        state = state.Write(BasisAddress, new byte[18]).Write(BasisAddress + 20, new byte[12]);
        Assert.Equal((ushort)0x1234, state.ReadWord(ScratchAddress + 6));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(ScratchAddress + 14));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(BasisAddress + 18));
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleStare45State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal(NativeBattleTestSnapshots.Json(state), NativeBattleTestSnapshots.Json(restored));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(BasisAddress + 18));
        Assert.Equal((ushort)0x1234, restored.ReadWord(ScratchAddress + 6));
    }

    [Fact]
    public void Six_exclusive_banks_have_actual_720_engine_capacity_and_can_transfer_to_active_Pools()
    {
        var state = NativeBattleStare45State.FromColdProcess();
        Assert.Equal((ushort)720, Pools[3].Stride);
        Assert.Equal(3, Pools[3].Capacity);
        (state with { InactiveBanks = Pools.Select(_ => (NativeBattleEffectArena?)null).ToImmutableArray() }).Validate();
        for (int index = 0; index < Pools.Length; index++)
        {
            var pool = Pools[index];
            Assert.Throws<InvalidDataException>(() => state.ReadWord(pool.Bank));
            Assert.Throws<InvalidDataException>(() => state.ReadWord(pool.Descriptor));
            Assert.Throws<InvalidDataException>(() => (state with { InactiveBanks = state.InactiveBanks.SetItem(index, NativeBattleEffectArena.Clear().MarkUnknown(pool.Stride * pool.Capacity, 1)) }).Validate());
        }
    }

    [Fact]
    public void Both_A_workspaces_are_distinct_and_native452_clear_leaves120_task_suffix_bytes()
    {
        var resources = Resources();
        byte[] first = Enumerable.Repeat((byte)0xA5, 720 - 0x94).ToArray();
        byte[] second = Enumerable.Repeat((byte)0x5A, 720 - 0x94).ToArray();
        var a = new NativeBattleGfAnimationWorkspace(first.ToImmutableArray()).Initialize7016B0(resources.ProgrammeA, ProgrammeAWorkspaceLength);
        var b = new NativeBattleGfAnimationWorkspace(second.ToImmutableArray()).Initialize7016B0(resources.ProgrammeA, ProgrammeAWorkspaceLength);
        Assert.Equal(120, NativeBattleStorageSnapshots.Capture(a).Data.Length - ProgrammeAWorkspaceLength);
        Assert.All(NativeBattleStorageSnapshots.Capture(a).Data.Skip(ProgrammeAWorkspaceLength), value => Assert.Equal((byte)0xA5, value));
        Assert.All(NativeBattleStorageSnapshots.Capture(b).Data.Skip(ProgrammeAWorkspaceLength), value => Assert.Equal((byte)0x5A, value));
        Assert.Equal(resources.ProgrammeA.Address, a.ResourceAddress);
        Assert.Equal(resources.ProgrammeA.Address, b.ResourceAddress);
        Assert.Equal(0, a.Frame);
    }

    internal static NativeBattleStare45Resources Resources()
    {
        byte[] camera = new byte[CameraLength];
        BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(2), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(4), 40);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), CameraLength);
        var resources = new NativeBattleStare45Resources(Source(ProgrammeAAddress, ProgrammeALength, 6, 30), Source(ProgrammeBAddress, ProgrammeBLength, 8, 40), new("FF8_EN:15C3584", camera.ToImmutableArray()), 0x61B1E);
        resources.Validate();
        return resources;

        static NativeBattleGfAnimationSource Source(uint address, int length, int count, int frames)
        {
            byte[] bytes = new byte[length];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, 4);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x100);
            for (int geometry = 0; geometry < 4; geometry++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8 + geometry * 4), 24);
            }
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), 20);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 1);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x100), count);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x104), frames);
            for (int node = 0; node < count; node++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x108 + node * 4), 8 + count * 4 + node * 8);
            }
            return new(address, bytes.ToImmutableArray());
        }
    }
}
