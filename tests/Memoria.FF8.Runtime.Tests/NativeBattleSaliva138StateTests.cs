using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleSaliva138Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSaliva138StateTests
{
    [Fact]
    public void Cold_loader_scalars_and_unknown_import_have_distinct_source_owners()
    {
        NativeBattleSaliva138State cold = NativeBattleSaliva138State.FromColdProcess();
        NativeBattleSaliva138State unknown = NativeBattleSaliva138State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.True(cold.PendingColdSources);
        Assert.Null(unknown.PendingColdSources);
        Assert.Null(cold.Mesh);
        Assert.Null(cold.ScaleMatrix32);
        Assert.Equal(0u, cold.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => cold.ReadMesh());
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(MatrixAddress));
    }

    [Fact]
    public void Reached_matrix_word_write_materializes_only_that_word_and_survives_JSON()
    {
        NativeBattleSaliva138State state = NativeBattleSaliva138State.FromUnknown().WriteWord(MatrixAddress + 16, 4096).WriteDword(WorkingGlobal, 0x20DFAB8);
        NativeBattleSaliva138State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleSaliva138State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Null(restored.PendingColdSources);
        Assert.Equal((ushort)4096, restored.ReadWord(MatrixAddress + 16));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(MatrixAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(MatrixAddress + 18));
        Assert.Equal(0x20DFAB8u, restored.ReadDword(WorkingGlobal));
        Assert.Equal(NativeBattleTestSnapshots.Json(state.WriteWord(MatrixAddress + 16, 2048)), NativeBattleTestSnapshots.Json(restored.WriteWord(MatrixAddress + 16, 2048)));
    }

    [Fact]
    public void Masked_matrix_copy_and_allocated_ranges_keep_one_owner()
    {
        NativeBattleEffectArena word = NativeBattleEffectArena.Clear().MarkUnknown(0, 4).Write(0, BitConverter.GetBytes((ushort)123));
        NativeBattleSaliva138State state = NativeBattleSaliva138State.FromUnknown().WriteView(MatrixAddress + 16, word, 4);
        Assert.Equal((ushort)123, state.ReadWord(MatrixAddress + 16));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(MatrixAddress + 18));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(0x20DFAB8, 1));
        Assert.Equal(FirstPacketOffset, MorphOffset + MorphBytes);
        Assert.Equal(MorphOffset, PointsOffset + PointStride * PointCount);
        Assert.Equal(PacketStep, SecondPacketOffset - FirstPacketOffset);
    }

    [Theory]
    [InlineData(0x2520C58u)]
    [InlineData(0x2520C68u)]
    [InlineData(0x2520C70u)]
    [InlineData(0x2520C80u)]
    [InlineData(0x2520C90u)]
    [InlineData(0x2520CA0u)]
    [InlineData(0x2520CB0u)]
    public void Root_and_descriptor_bytes_are_excluded_from_scalar_storage(uint address)
    {
        NativeBattleSaliva138State state = NativeBattleSaliva138State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }
}
