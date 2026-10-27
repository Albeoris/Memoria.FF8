using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect101StateTests
{
    [Fact]
    public void Cold_loader_zero_and_imported_unknown_are_distinct_for_scale_and_cursor()
    {
        NativeBattleEffect101State cold = NativeBattleEffect101State.FromColdProcess();
        NativeBattleEffect101State unknown = NativeBattleEffect101State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(NativeBattleEffect101Profile.CursorGlobal));
        Assert.Equal(new byte[12], cold.Read(NativeBattleEffect101Profile.ScaleAddress, 12));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(NativeBattleEffect101Profile.ScaleAddress));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(NativeBattleEffect101Profile.WorkingGlobal));
    }
    [Fact]
    public void Partial_pointed_scale_knownness_and_warm_values_continue_after_JSON()
    {
        NativeBattleEffectArena scale = NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(0, BitConverter.GetBytes(-12345)).Write(4, BitConverter.GetBytes((short)4096));
        NativeBattleEffect101State state = NativeBattleEffect101State.FromUnknown().WriteView(NativeBattleEffect101Profile.ScaleAddress, scale, 12).WriteDword(NativeBattleEffect101Profile.TargetGlobal, 0x1D972C0);
        NativeBattleEffect101State restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect101State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        restored.Validate();
        Assert.Equal(unchecked((uint)-12345), restored.ReadDword(NativeBattleEffect101Profile.ScaleAddress));
        Assert.Equal((ushort)4096, restored.ReadWord(NativeBattleEffect101Profile.ScaleAddress + 4));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(NativeBattleEffect101Profile.ScaleAddress + 4));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(NativeBattleEffect101Profile.ScaleAddress + 8));
        Assert.Equal(0x1D972C0u, restored.ReadDword(NativeBattleEffect101Profile.TargetGlobal));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state.WriteDword(NativeBattleEffect101Profile.ScaleAddress + 8, 123))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored.WriteDword(NativeBattleEffect101Profile.ScaleAddress + 8, 123))));
    }
    [Theory]
    [InlineData(0x2544E38u)]
    [InlineData(0x2544E48u)]
    [InlineData(0x2544E58u)]
    [InlineData(0x2544E68u)]
    [InlineData(0x2544E74u)]
    public void Task_descriptors_root_bank_and_outside_bytes_have_no_second_scalar_owner(uint address)
    {
        NativeBattleEffect101State state = NativeBattleEffect101State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }
}
