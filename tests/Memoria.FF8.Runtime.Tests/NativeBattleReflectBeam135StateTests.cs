using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleReflectBeam135Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleReflectBeam135StateTests
{
    [Fact]
    public void Cold_loader_zero_includes_actual_scale_matrix_without_duplicate_owner_or_seed()
    {
        NativeBattleReflectBeam135State cold = NativeBattleReflectBeam135State.FromColdProcess();
        cold.Validate();
        Assert.Equal(new byte[32], cold.Read(ScaleMatrixGlobal, 32));
        foreach (uint address in new[] { CasterGlobal, InitializerGlobal, WorkingGlobal, ParametersGlobal, CursorGlobal })
        {
            Assert.Equal(0u, cold.ReadDword(address));
        }
    }

    [Theory]
    [InlineData(0x2520DE0u)]
    [InlineData(0x2520DF0u)]
    [InlineData(0x2520DF8u)]
    [InlineData(0x2520E08u)]
    [InlineData(0x2520E18u)]
    [InlineData(0x2520E28u)]
    [InlineData(0x2520E38u)]
    [InlineData(0x2520E68u)]
    public void Original_root_task_and_all_descriptors_are_excluded_from_scalar_owner(uint address)
    {
        NativeBattleReflectBeam135State state = NativeBattleReflectBeam135State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }

    [Fact]
    public void Unknown_import_diagonal_writes_preserve_unwritten_matrix_bytes_and_JSON_next()
    {
        NativeBattleReflectBeam135State state = NativeBattleReflectBeam135State.FromUnknown().WriteWord(ScaleMatrixGlobal, 4096).WriteWord(ScaleMatrixGlobal + 8, 2048).WriteWord(ScaleMatrixGlobal + 16, 1024);
        Assert.Throws<NotSupportedException>(() => state.Read(ScaleMatrixGlobal, 32));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CasterGlobal));
        (byte[] bytes, bool[] known) = state.ReadView(ScaleMatrixGlobal, 32).ReadSnapshot(0, 32);
        Assert.Equal(new[] { 0, 1, 8, 9, 16, 17 }, known.Select((value, index) => (value, index)).Where(item => item.value).Select(item => item.index));
        NativeBattleReflectBeam135State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleReflectBeam135State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        NativeBattleEffectArena continuation = NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(20, BitConverter.GetBytes(123));
        NativeBattleReflectBeam135State expected = state.WriteView(ScaleMatrixGlobal, continuation, 32);
        NativeBattleReflectBeam135State actual = restored.WriteView(ScaleMatrixGlobal, continuation, 32);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
        Assert.Equal(123u, actual.ReadDword(ScaleMatrixGlobal + 20));
        Assert.Throws<NotSupportedException>(() => actual.ReadWord(ScaleMatrixGlobal));
        Assert.Equal((byte)0, bytes[0]);
    }

    [Fact]
    public void Forged_known_descriptor_region_is_rejected_and_masked_scalar_write_is_retained()
    {
        NativeBattleReflectBeam135State state = NativeBattleReflectBeam135State.FromColdProcess();
        NativeBattleReflectBeam135State invalid = state with { Globals = state.Globals.Write(checked((int)(ParticleDescriptor - BaseAddress)), new byte[4]) };
        Assert.Throws<InvalidDataException>(() => invalid.Validate());
        NativeBattleEffectArena partial = NativeBattleEffectArena.Clear().MarkUnknown(0, 4).Write(0, new byte[] { 0x12, 0x34 });
        state = state.WriteView(CursorGlobal, partial, 4);
        Assert.Equal((ushort)0x3412, state.ReadWord(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CursorGlobal));
    }
}
