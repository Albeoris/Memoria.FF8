using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleOilShot136Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOilShot136StateTests
{
    [Fact]
    public void Current_matrix_uses_single_BSS_owner_and_retains_partial_knownness()
    {
        NativeBattleOilShot136State cold = NativeBattleOilShot136State.FromColdProcess();
        Assert.All(cold.ReadCurrentMatrix().Known, Assert.True);
        Assert.All(cold.ReadCurrentMatrix().Bytes, value => Assert.Equal((byte)0, value));
        NativeBattleOilShot136State unknown = NativeBattleOilShot136State.FromUnknown();
        Assert.All(unknown.ReadCurrentMatrix().Known, Assert.False);
        NativeBattleOilShot136State written = unknown.WriteWord(CurrentMatrixAddress + 16, 1234);
        NativeBattleOilShot136State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleOilShot136State>(NativeBattleTestSnapshots.Json(written))!;
        restored.Validate();
        Assert.Equal((ushort)1234, restored.ReadWord(CurrentMatrixAddress + 16));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(CurrentMatrixAddress));
        Assert.Equal(written.ReadCurrentMatrix().Bytes.ToArray(), restored.ReadCurrentMatrix().Bytes.ToArray());
        Assert.Equal(written.ReadCurrentMatrix().Known.ToArray(), restored.ReadCurrentMatrix().Known.ToArray());
    }

    [Fact]
    public void Cold_loader_scalars_and_unknown_import_have_distinct_source_owners()
    {
        NativeBattleOilShot136State cold = NativeBattleOilShot136State.FromColdProcess();
        NativeBattleOilShot136State unknown = NativeBattleOilShot136State.FromUnknown();
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
        NativeBattleOilShot136State state = NativeBattleOilShot136State.FromUnknown().WriteWord(MatrixAddress + 16, 4096).WriteDword(WorkingGlobal, 0x20DFAB8);
        NativeBattleOilShot136State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleOilShot136State>(NativeBattleTestSnapshots.Json(state))!;
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
        NativeBattleOilShot136State state = NativeBattleOilShot136State.FromUnknown().WriteView(MatrixAddress + 16, word, 4);
        Assert.Equal((ushort)123, state.ReadWord(MatrixAddress + 16));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(MatrixAddress + 18));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(0x20DFAB8, 1));
        Assert.Equal(FirstPacketOffset, MorphOffset + MorphBytes);
        Assert.Equal(MorphOffset, MainOffset + MainStride);
        Assert.Equal(SimpleSpriteOffset, LargeParticleOffset + LargeParticleStride * LargeParticleCount);
        Assert.Equal(EmitterOffset, SimpleSpriteOffset + SimpleSpriteStride * SimpleSpriteCount);
        Assert.Equal(MovingSpriteOffset, EmitterOffset + EmitterStride * EmitterCount);
        Assert.Equal(MainOffset, MovingSpriteOffset + MovingSpriteStride * MovingSpriteCount);
        Assert.Equal(PacketStep, SecondPacketOffset - FirstPacketOffset);
    }

    [Theory]
    [InlineData(0x2520D38u)]
    [InlineData(0x2520D48u)]
    [InlineData(0x2520D50u)]
    [InlineData(0x2520D60u)]
    [InlineData(0x2520D70u)]
    [InlineData(0x2520D80u)]
    [InlineData(0x2520D90u)]
    [InlineData(0x2520DA0u)]
    public void Root_and_descriptor_bytes_are_excluded_from_scalar_storage(uint address)
    {
        NativeBattleOilShot136State state = NativeBattleOilShot136State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }
}
