namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBadBreath131FlashCaptureTests
{
    [Fact]
    public void Envelope_capture_preserves_native_factor_packets_and_remains_discrete_screen_geometry()
    {
        NativeBattlePilotVisualIdentity identity = new(131, 0x2521088, 0, 3, 0) { ResourceGeneration = 0x700150 };
        NativeBattleGfGeometryBatch native = NativeBattleModuleRenderCommands.Emit700150(20, 64, 64, 0, 14, 22, 14);
        NativeBattleGfGeometryBatch captured = NativeBattleModuleRenderCommands.Emit700150(20, 64, 64, 0, 14, 22, 14, identity);
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        NativeBattlePilotVisualGeometry geometry = Assert.Single(captured.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal(identity, geometry.Identity);
        Assert.True(geometry.ScreenSpace);
        Assert.False(geometry.Continuous);
    }
}
