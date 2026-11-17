using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEscapeFadeTests
{
    [Fact]
    public void Native_escape_fade_packet_uses_the_full_viewport_and_original_blend_mode()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeBattlePreparedPrimitiveFrame fade = NativeBattleOutcomeCallbacks.PrepareFade501D10(68, assets.ReadNativePrimitiveTables());
        var quad = Assert.Single(fade.LastCommands, command => command.Pass == 2 && command.Command.Opcode == 0x2A);
        Assert.Equal(2, quad.DrawMode.BlendMode);
        Assert.Equal(NativeBattleScreenCoverage.Full, quad.Command.ScreenCoverage);
        Assert.Equal(new uint[] { 0x2A3C3C3C, 0, 320, 216u << 16, 320 | 216u << 16 }, quad.Command.Words);
    }
}
