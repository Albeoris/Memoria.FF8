using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBadBreath113CaptureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Body_capture_uses_final_native_sway_screen_corners_and_exact_depth(bool quad)
    {
        uint Pack(int x, int y) => (uint)(ushort)x | (uint)(ushort)y << 16;
        NativeBattleModuleProjectedVertex[] source = [new(Pack(100, 100), 1000, 0), new(Pack(300, 100), 1100, 0), new(Pack(100, 300), 1200, 0), new(Pack(300, 300), 1300, 0)];
        ImmutableArray<short> wave = Enumerable.Repeat((short)4096, 4096).ToImmutableArray();
        NativeBattleProjectedVertex[] swayed = source.Select(p => NativeBattleBadBreath113.Sway6CB7D0(p, 0, 0, 512, 0, wave, default)).ToArray();
        byte[] bytes = new byte[quad ? 20 : 16];
        for (int corner = 0; corner < (quad ? 4 : 3); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(corner * 2), (ushort)corner);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0x12340201);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0x02040302);
        NativeBattleRawPolygon polygon = new(quad, bytes.ToImmutableArray());
        NativeBattleGfGeometryBatch native = NativeBattleEffect107Geometry.Emit6BCB50([polygon], swayed, 0x808080, null);
        NativeBattlePilotVisualIdentity identity = new(113, 0x2521898, 0, 12, 2) { ResourceGeneration = 0x1000000 };
        NativeBattleGfGeometryBatch captured = NativeBattleEffect107Geometry.Emit6BCB50([polygon], swayed, 0x808080, null, pilotIdentity: identity);
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        NativeBattlePilotVisualGeometry geometry = Assert.Single(captured.Attempts).Primitive!.PilotGeometry!;
        Assert.True(geometry.ScreenSpace);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(125f, geometry.A.X);
        Assert.Equal(375f, geometry.B.X);
        Assert.Equal(1000f, geometry.SourceDepths!.Value.X);
        Assert.Equal(identity.ResourceGeneration, geometry.Identity.ResourceGeneration);
        Assert.Equal(quad ? 4 : 3, geometry.CornerCount);
    }
}
