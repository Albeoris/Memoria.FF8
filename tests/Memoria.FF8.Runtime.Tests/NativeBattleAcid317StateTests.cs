using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAcid317StateTests
{
    [Fact]
    public void Cold_globals_have_loader_zero_but_unknown_inputs_do_not_gain_defaults()
    {
        NativeBattleAcid317State cold = NativeBattleAcid317State.FromColdProcess();
        Assert.Equal(0u, cold.ReadDword(0x25054D8));
        Assert.Equal(0u, cold.ReadDword(0x25054DC));
        Assert.Throws<NotSupportedException>(() => NativeBattleAcid317State.FromUnknown().ReadDword(0x25054D8));
    }

    [Theory]
    [InlineData(0x25054A0u)]
    [InlineData(0x25054B0u)]
    [InlineData(0x25054E0u)]
    [InlineData(0x25054F0u)]
    [InlineData(0x2505500u)]
    [InlineData(0x2505510u)]
    public void Pools_and_descriptors_are_not_duplicated_in_globals(uint address)
    {
        NativeBattleAcid317State state = NativeBattleAcid317State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }

    [Fact]
    public void Warm_view_unknown_padding_and_private_cursor_survive_snapshot()
    {
        NativeBattleEffectArena packed = NativeBattleEffectArena.Clear().Write(20, BitConverter.GetBytes(123456)).MarkUnknown(18, 2);
        NativeBattleAcid317State state = NativeBattleAcid317State.FromColdProcess().WriteView(0x25054B8, packed, 32).WriteDword(0x25054B4, 0x20F41C0).WriteDword(0x25054D8, 0xCAFEBABE);
        state.Validate();
        string json = NativeBattleTestSnapshots.Json(state);
        NativeBattleAcid317State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleAcid317State>(json)!;
        restored.Validate();
        Assert.Equal(json, NativeBattleTestSnapshots.Json(restored));
        Assert.Equal(0x20F41C0u, restored.ReadDword(0x25054B4));
        Assert.Equal(0xCAFEBABEu, restored.ReadDword(0x25054D8));
        NativeBattleEffectArena view = restored.ReadView(0x25054B8, 32);
        Assert.Equal(123456, BitConverter.ToInt32(view.Read(20, 4)));
        Assert.Throws<NotSupportedException>(() => view.Read(18, 2));
    }
}
