using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOilBlast137StateTests
{
    [Fact]
    public void Cold_scalars_are_zero_but_matrix_requires_its_one_time_original_seed()
    {
        NativeBattleOilBlast137State cold = NativeBattleOilBlast137State.FromColdProcess();
        NativeBattleOilBlast137State unknown = NativeBattleOilBlast137State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.True(cold.ColdMatrixSourcePending);
        Assert.Null(unknown.ColdMatrixSourcePending);
        Assert.Equal(0u, cold.ReadDword(NativeBattleOilBlast137Profile.CursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(NativeBattleOilBlast137Profile.CursorGlobal));
        Assert.Throws<NotSupportedException>(() => cold.ReadWord(NativeBattleOilBlast137Profile.MatrixAddress));
        Assert.Throws<NotSupportedException>(() => unknown.WriteWord(NativeBattleOilBlast137Profile.MatrixAddress, 4096));
    }

    [Fact]
    public void Warm_matrix_partial_padding_and_scalar_next_write_survive_JSON()
    {
        NativeBattleOilBlast137State state = NativeBattleOilBlast137State.FromUnknown() with { ColdMatrixSourcePending = false, Matrix32 = NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(0, BitConverter.GetBytes((short)1234)) };
        state = state.WriteDword(NativeBattleOilBlast137Profile.WorkingGlobal, 0x20DFAB8);
        NativeBattleOilBlast137State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleOilBlast137State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal((ushort)1234, restored.ReadWord(NativeBattleOilBlast137Profile.MatrixAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(NativeBattleOilBlast137Profile.MatrixAddress + 18));
        Assert.Equal(0x20DFAB8u, restored.ReadDword(NativeBattleOilBlast137Profile.WorkingGlobal));
        Assert.Equal(NativeBattleTestSnapshots.Json(state.WriteWord(NativeBattleOilBlast137Profile.MatrixAddress + 8, 4096)), NativeBattleTestSnapshots.Json(restored.WriteWord(NativeBattleOilBlast137Profile.MatrixAddress + 8, 4096)));
        Assert.Equal(NativeBattleOilBlast137Profile.FirstPacketOffset, NativeBattleOilBlast137Profile.MorphOffset + NativeBattleOilBlast137Profile.MorphBytes);
        Assert.Equal(NativeBattleOilBlast137Profile.PacketStep, NativeBattleOilBlast137Profile.SecondPacketOffset - NativeBattleOilBlast137Profile.FirstPacketOffset);
    }

    [Theory]
    [InlineData(0x2520CD0u)]
    [InlineData(0x2520CE0u)]
    [InlineData(0x2520CE8u)]
    [InlineData(0x2520CF8u)]
    [InlineData(0x2520D08u)]
    [InlineData(0x2520D18u)]
    [InlineData(0x2520D28u)]
    public void Root_bank_and_all_descriptors_have_no_second_scalar_owner(uint address)
    {
        NativeBattleOilBlast137State state = NativeBattleOilBlast137State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }
}
