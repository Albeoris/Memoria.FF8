using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleDisolvingAcid132Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDisolvingAcid132StateTests
{
    [Fact]
    public void Cold_scalars_and_unknown_import_keep_distinct_knownness()
    {
        NativeBattleDisolvingAcid132State cold = NativeBattleDisolvingAcid132State.FromColdProcess();
        NativeBattleDisolvingAcid132State unknown = NativeBattleDisolvingAcid132State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(TemporaryGlobal));
        Assert.Equal(0u, cold.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(TemporaryGlobal));
        Assert.Throws<InvalidDataException>(() => cold.ReadWord(EmissionVectorsAddress));
    }

    [Fact]
    public void Reached_scalar_write_materializes_only_its_actual_word_after_JSON()
    {
        NativeBattleDisolvingAcid132State state = NativeBattleDisolvingAcid132State.FromUnknown().WriteWord(TemporaryGlobal + 2, 1).WriteDword(WorkingGlobal, 0x20DFAB8);
        NativeBattleDisolvingAcid132State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDisolvingAcid132State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal((ushort)1, restored.ReadWord(TemporaryGlobal + 2));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(TemporaryGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(CursorGlobal));
        Assert.Equal(0x20DFAB8u, restored.ReadDword(WorkingGlobal));
        Assert.Equal(NativeBattleTestSnapshots.Json(state.WriteWord(TemporaryGlobal + 2, 0)), NativeBattleTestSnapshots.Json(restored.WriteWord(TemporaryGlobal + 2, 0)));
    }

    [Fact]
    public void Masked_scalar_copy_keeps_unknown_lanes_and_excludes_other_physical_owners()
    {
        NativeBattleEffectArena word = NativeBattleEffectArena.Clear().MarkUnknown(0, 4).Write(0, BitConverter.GetBytes((ushort)123));
        NativeBattleDisolvingAcid132State state = NativeBattleDisolvingAcid132State.FromUnknown().WriteView(TemporaryGlobal, word, 4);
        Assert.Equal((ushort)123, state.ReadWord(TemporaryGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(TemporaryGlobal + 2));
        Assert.Equal(word.ReadSnapshot(0, 4).Known, state.ReadView(TemporaryGlobal, 4).ReadSnapshot(0, 4).Known);
        Assert.Throws<InvalidDataException>(() => state.WriteDword(EmissionVectorsAddress + 14, 1));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(0x20DFAB8, 1));
    }

    [Fact]
    public void Pools_points_morph_and_packet_banks_are_contiguous_physical_ranges()
    {
        Assert.Equal(SimpleSpriteOffset, SpriteOffset + SpriteStride * SpriteCount);
        Assert.Equal(CurveOffset, SimpleSpriteOffset + SimpleSpriteStride * SimpleSpriteCount);
        Assert.Equal(ActOffset, CurveOffset + CurveStride * CurveCount);
        Assert.Equal(MainOffset, ActOffset + ActStride * ActCount);
        Assert.Equal(PointsOffset, MainOffset + MainStride * MainCount);
        Assert.Equal(MorphOffset, PointsOffset + PointStride * PointCount);
        Assert.Equal(FirstPacketOffset, MorphOffset + MorphBytes);
        Assert.Equal(68 * 8, MorphBytes);
        Assert.Equal(PacketStep, SecondPacketOffset - FirstPacketOffset);
    }

    [Theory]
    [InlineData(0x2520F70u)]
    [InlineData(0x2520F80u)]
    [InlineData(0x2520F8Cu)]
    [InlineData(0x2520F90u)]
    [InlineData(0x2520FA0u)]
    [InlineData(0x2520FB0u)]
    [InlineData(0x2520FC0u)]
    [InlineData(0x2520FD0u)]
    [InlineData(0x2520FE0u)]
    public void Root_and_descriptor_bytes_are_excluded_from_scalar_storage(uint address)
    {
        NativeBattleDisolvingAcid132State state = NativeBattleDisolvingAcid132State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }
}
