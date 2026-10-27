using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleGravija214Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGravija214StateTests
{
    [Fact]
    public void Cold_BSS_is_zero_without_resources_or_an_invented_identity_matrix()
    {
        var state = NativeBattleGravija214State.FromColdProcess();
        state.Validate();
        Assert.Equal(new byte[32], state.Read(MatrixAddress, 32));
        Assert.Equal(new byte[EntryCopyLength], state.Read(CopiedMeshAddress, EntryCopyLength));
        Assert.Equal(0u, state.ReadDword(ParametersGlobal));
        Assert.NotNull(state.RootBankStorage);
        Assert.Equal(0x7740, EntryCopyLength);
        Assert.Equal(7632, MeshLength);
        Assert.Equal(CopiedMeshAddress + EntryCopyLength, ParametersGlobal);
    }

    [Fact]
    public void Native_entry_copies_full_original_source_and_warm_Advance_has_no_reseed()
    {
        var resources = Resources();
        resources.Validate();
        var state = NativeBattleGravija214State.FromUnknown().CopyEntrySnapshot(resources);
        Assert.Equal(resources.EntryCopySeed.Data.ToArray(), state.Read(CopiedMeshAddress, EntryCopyLength));
        state = state.WriteWord(CopiedMeshAddress + 8, 0x1234);
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleGravija214State>(NativeBattleTestSnapshots.Json(state))!;
        Assert.Equal((ushort)0x1234, restored.ReadWord(CopiedMeshAddress + 8));
        Assert.Equal((ushort)0, restored.CopyEntrySnapshot(resources).ReadWord(CopiedMeshAddress + 8));
        Assert.Equal((byte)0, resources.EntryCopySeed.Data[8]);
    }

    [Fact]
    public void Sparse_state_excludes_pool_descriptors_COMMON_and_cross_owner_reads()
    {
        var state = NativeBattleGravija214State.FromColdProcess();
        foreach (uint address in new[] { RootBank, RootDescriptor, 0x2510118u, 0x2510128u, ArenaAddress, ArenaAddress + ScratchOffset, MatrixAddress + 32, SourceMeshAddress })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Throws<InvalidDataException>(() => state.Read(CopiedMeshAddress + EntryCopyLength - 2, 4));
        Assert.Throws<InvalidDataException>(() => (state with { CopiedSource = state.CopiedSource.Write(EntryCopyLength, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(RootBankLength, 1) }).Validate());
        Assert.Equal(new[] { 20, 28, 804 }, Pools.Select(pool => (int)pool.Stride).ToArray());
        Assert.Equal(new[] { 1, 64, 1 }, Pools.Select(pool => (int)pool.Capacity).ToArray());
    }

    [Fact]
    public void Partial_matrix_and_current_mesh_writes_retain_unknown_lanes_through_JSON()
    {
        var state = NativeBattleGravija214State.FromUnknown().WriteWord(MatrixAddress, 4096).WriteWord(MatrixAddress + 8, 4096).WriteDword(MatrixAddress + 20, 160).WriteDword(MatrixAddress + 24, 108).Write(CopiedMeshAddress + 8, new byte[6]) with { RootBankStorage = null };
        state.Validate();
        Assert.Throws<NotSupportedException>(() => state.ReadWord(MatrixAddress + 16));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(MatrixAddress + 18));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(CopiedMeshAddress + 14));
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleGravija214State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal(NativeBattleTestSnapshots.Json(state), NativeBattleTestSnapshots.Json(restored));
        Assert.Null(restored.RootBankStorage);
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(CopiedMeshAddress + 14));
    }

    internal static NativeBattleGravija214Resources Resources()
    {
        byte[] copy = new byte[EntryCopyLength];
        Put(copy, 0, 1936);
        Put(copy, 4, VertexCount);
        Put(copy, 1952, 24);
        Put(copy, 2436, 216);
        byte[] programme = new byte[ProgrammeLength];
        Put(programme, 0, 7);
        Put(programme, 4, 0x3430);
        int[] vertices = [72, 72, 128, 72, 25, 25, 25];
        int[] shapes = [2, 2, 2, 1, 1, 1, 1];
        int[] lengths = [2348, 2348, 4396, 2348, 628, 628, 628];
        int cursor = 36;
        for (int index = 0; index < 7; index++)
        {
            Put(programme, 8 + index * 4, checked((uint)cursor));
            int polygon = 12 + vertices[index] * shapes[index] * 8;
            Put(programme, cursor, checked((uint)polygon));
            Put(programme, cursor + 4, checked((uint)vertices[index]));
            Put(programme, cursor + 8, checked((uint)shapes[index]));
            int group = index == 2 ? 5 : index == 3 ? 7 : 3;
            Put(programme, cursor + polygon + group * 4, index == 2 ? 96u : index < 4 ? 48u : 16u);
            cursor += lengths[index];
        }
        Put(programme, 0x3430, 11);
        Put(programme, 0x3434, 74);
        for (int index = 0; index < 11; index++)
        {
            Put(programme, 0x3438 + index * 4, checked((uint)(52 + index * 8)));
        }
        byte[] sprite = new byte[SpriteLength];
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 50);
        for (int frame = 0; frame <= 50; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + frame * 2), 112);
        }
        byte[] camera = new byte[CameraLength];
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(2), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(4), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), CameraLength);
        return new(new("FF8_EN:1172D00", copy.ToImmutableArray()), new(ProgrammeAddress, programme.ToImmutableArray()), new(0, SpriteAddress, 0, 0, sprite.ToImmutableArray()), new("FF8_EN:1178938", camera.ToImmutableArray()), 0x642BF, new ushort[192].ToImmutableArray(), new short[4096].ToImmutableArray());

        static void Put(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    }
}
