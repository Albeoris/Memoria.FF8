namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSummonModelTests
{
    private static readonly NativeBattleSummonColors Colors = new(0x00808080, 0x32808080, 1f / 4096, 4503601774854144d);

    [Fact]
    public void Hiding_preserves_the_allocation_and_becomes_ready_only_at_phase_fifteen()
    {
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.StartService(new(1, 3) { Flags2 = 0, Flags8 = 0, Kind5 = 0 }, 11);
        NativeBattleGraphicsModel frozen = NativeBattleSummonModels.Advance(model, true, Colors);
        Assert.Equal((byte)0, frozen.Phase6);
        Assert.Equal(0x02808080u, frozen.Color40);
        Assert.Equal(0x32808080u, frozen.Color44);
        for (int callback = 0; callback < 15; callback++)
        {
            model = NativeBattleSummonModels.Advance(model, false, Colors);
            Assert.Equal((byte)11, model.Kind5);
            Assert.Equal((ushort)0, model.Flags0 & 4);
        }
        model = NativeBattleSummonModels.Advance(model, true, Colors);
        Assert.Equal((byte)0, model.Kind5);
        Assert.Equal((ushort)7, model.Flags0);
        Assert.Equal((ushort)0x80, model.Flags2);
        Assert.Equal(0u, model.Color40);
        Assert.Equal((byte)15, model.Phase6);
    }

    [Fact]
    public void Resume_uses_its_original_preincrement_even_while_graphics_are_frozen()
    {
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.StartService(new(1, 7) { Flags2 = 0x80, Flags8 = 0, Kind5 = 0, Color40 = 0 }, 12);
        Assert.Equal((ushort)3, model.Flags0);
        Assert.Equal((ushort)0, model.Flags2);
        NativeBattleGraphicsModel frozen = NativeBattleSummonModels.Advance(model, true, Colors);
        Assert.Equal((byte)1, frozen.Phase6);
        Assert.Equal((byte)12, frozen.Kind5);
        for (int callback = 0; callback < 8; callback++)
        {
            model = NativeBattleSummonModels.Advance(model, false, Colors);
        }
        Assert.Equal((byte)0, model.Kind5);
        Assert.Equal((byte)16, model.Phase6);
        Assert.Equal(0x32808080u, model.Color44);
        Assert.Equal((ushort)3, model.Flags0);
    }
}
