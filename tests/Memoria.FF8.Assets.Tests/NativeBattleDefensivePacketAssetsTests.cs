using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDefensivePacketAssetsTests
{
    [Theory]
    [InlineData(0, 48, 36, 0x00FFFFu, 0x007F7Fu, 0x003F3Fu)]
    [InlineData(1, 24, 12, 0x774700u, 0x3B2300u, 0x1D1100u)]
    [InlineData(2, 60, 0, 0x5400DFu, 0x2A006Fu, 0x150037u)]
    public void Original_defensive_meshes_emit_their_Gouraud_forms_with_570650_phase_context(byte variant, int polygons, int quads, uint full, uint half, uint quarter)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleDefensiveResource resource = assets.ReadNativeBattleDefensiveResources().Single(item => item.Variant == variant);
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 20000, 4096), 216, 160, 108);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Defensive, 0) { Anchor = new(0, 0, 0, 0), Variant = variant, Scale = 4096 };
        NativeBattleModulePacketBatch visible = NativeBattleDefensivePackets.Emit570650(draw with { Phase = 2 }, resource, projection);
        Assert.Equal(polygons, visible.Primitives.Length);
        Assert.Equal(quads, visible.Primitives.Count(packet => packet.Primitive.Words.Length == 9));
        Assert.All(visible.Primitives, packet =>
        {
            Assert.Contains(packet.Primitive.Words.Length, new[] { 7, 9 });
            Assert.Equal((ushort)0, packet.Flags20);
        });
        foreach ((ushort phase, uint firstColor) in new (ushort, uint)[] { (0, 0), (1, half), (2, full), (3, full), (4, full), (7, quarter) })
        {
            NativeBattleModulePacketBatch actual = NativeBattleDefensivePackets.Emit570650(draw with { Phase = phase }, resource, projection);
            Assert.Equal(0x32000000 | firstColor, actual.Primitives[0].Primitive.Words[1]);
            Assert.Equal(visible.Primitives.Length, actual.Primitives.Length);
            if (phase == 0)
            {
                Assert.All(actual.Primitives, packet => Assert.Equal(0u, packet.Primitive.Words[1] & 0xFFFFFF));
            }
        }
        Assert.Contains(visible.Primitives, packet => (packet.Primitive.Words[1] & 0xFFFFFF) != 0);
    }
}
