using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverNativeCureProgramTests
{
    [Fact]
    public void Cure_foreign_triangle_overlay_keeps_native_eighth_pixel_texture_window()
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 8192, 4096);
        NativeBattleGeometryPrimitive overlay = Assert.IsType<NativeBattleGeometryPrimitive>(NativeBattleCureActorPackets.ProjectTriangleOverlay8D7430(new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), matrix, matrix, new(matrix, 512, 20, 15), 0, 17));
        Assert.Equal(528, overlay.Bucket);
        Assert.Equal(0x36808080u, overlay.Words[1]);
        Assert.Equal(0x3C54C040u, overlay.Words[3]);
        Assert.Equal(0x00B8C072u, overlay.Words[6]);
        Assert.Equal(0xF240u, overlay.Words[9]);
        Assert.Null(NativeBattleCureActorPackets.ProjectTriangleOverlay8D7430(new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), matrix, matrix, new(matrix, 512, 160, 120), 0, 17));
    }

}
