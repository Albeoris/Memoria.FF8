using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleGastricJuice128Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGastricJuice128StateTests
{
    [Fact]
    public void Cold_BSS_and_unknown_import_keep_actual_matrix_and_curve_parameter_knownness()
    {
        NativeBattleGastricJuice128State cold = NativeBattleGastricJuice128State.FromColdProcess();
        NativeBattleGastricJuice128State unknown = NativeBattleGastricJuice128State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(CursorGlobal));
        Assert.Equal(new byte[8], cold.Read(CurveParametersGlobal, 8));
        Assert.All(cold.ReadCurrentMatrix().Known, Assert.True);
        Assert.All(cold.ReadCurrentMatrix().Bytes, value => Assert.Equal((byte)0, value));
        Assert.All(unknown.ReadCurrentMatrix().Known, Assert.False);
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(CurveParametersGlobal));
    }

    [Fact]
    public void Current_matrix_and_curve_parameters_survive_JSON_without_duplicate_owners()
    {
        NativeBattleGastricJuice128State state = NativeBattleGastricJuice128State.FromUnknown().WriteWord(CurrentMatrixAddress + 16, 4096).WriteDword(CurveParametersGlobal, 1234);
        NativeBattleGastricJuice128State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleGastricJuice128State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal((ushort)4096, restored.ReadWord(CurrentMatrixAddress + 16));
        Assert.Equal(1234u, restored.ReadDword(CurveParametersGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(CurrentMatrixAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(CurrentMatrixAddress + 18));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(CurveParametersGlobal + 4));
        Assert.Equal(NativeBattleTestSnapshots.Json(state), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void Masked_matrix_copy_preserves_unknown_padding_and_excludes_Arena_and_resources()
    {
        NativeBattleEffectArena word = NativeBattleEffectArena.Clear().MarkUnknown(0, 4).Write(0, BitConverter.GetBytes((ushort)4096));
        NativeBattleGastricJuice128State state = NativeBattleGastricJuice128State.FromUnknown().WriteView(CurrentMatrixAddress + 16, word, 4);
        Assert.Equal((ushort)4096, state.ReadWord(CurrentMatrixAddress + 16));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(CurrentMatrixAddress + 18));
        Assert.Equal(word.ReadSnapshot(0, 4).Known, state.ReadView(CurrentMatrixAddress + 16, 4).ReadSnapshot(0, 4).Known);
        Assert.Throws<InvalidDataException>(() => state.WriteDword(MainActAddress, 1));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(0x20DFAB8, 1));
    }

    [Fact]
    public void Pools_scratch_points_body_cache_and_private_banks_have_exact_physical_ranges()
    {
        Assert.Equal(CurveOffset, SpriteOffset + SpriteStride * SpriteCount);
        Assert.Equal(MainOffset, CurveOffset + CurveStride * CurveCount);
        Assert.Equal(MorphOffset, MainOffset + MainStride * MainCount);
        Assert.Equal(PointsOffset, MorphOffset + MorphBytes);
        Assert.Equal(BodyCacheOffset, PointsOffset + PointStride * PointCount);
        Assert.Equal(FirstPacketOffset, BodyCacheOffset + BodyCacheBytes);
        Assert.Equal(64 * 8, MorphBytes);
        Assert.Equal(PacketStep, SecondPacketOffset - FirstPacketOffset);
    }

    [Theory]
    [InlineData(0x25211F0u)]
    [InlineData(0x2521200u)]
    [InlineData(0x2521230u)]
    [InlineData(0x2521240u)]
    [InlineData(0x2521250u)]
    [InlineData(0x2521260u)]
    public void Root_and_all_descriptors_are_excluded_from_scalar_storage(uint address)
    {
        NativeBattleGastricJuice128State state = NativeBattleGastricJuice128State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }
}
