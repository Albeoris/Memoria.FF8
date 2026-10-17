using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>570650's original571240 mesh, matrix and572200 context.</summary>
public static class NativeBattleDefensivePackets
{
    public static NativeBattleModulePacketBatch Emit570650(NativeBattleModuleDraw draw, NativeBattleDefensiveResource resource, NativeBattleCameraProjection projection, NativeBattleSummonColors? colors = null)
    {
        if (draw.Kind != NativeBattleModuleDrawKind.Defensive || resource.Variant != draw.Variant)
        {
            throw new InvalidDataException("Native570650 requires its selected off_C81678 defensive mesh.");
        }
        short phase = unchecked((short)draw.Phase);
        uint flags = 0x33;
        int fade = 0;
        if (phase < 2)
        {
            flags = 0xF3;
            fade = (2 - phase) << 11;
        }
        else if (phase >= 4)
        {
            flags = 0xF3;
            fade = (phase - 4) << 10;
        }
        short scale = unchecked((short)draw.Scale);
        NativeBattleModuleDraw mesh = draw with { MeshRotation = new(0, draw.Rotation, draw.Orientation), MeshScale = new(scale, scale, scale), MeshFlags = flags, MeshFade = fade };
        return NativeBattleModulePackets.EmitMesh572200(mesh, new(checked((byte)(8 + resource.Variant)), resource.Address, resource.Bytes), projection, 17, 2, colors);
    }
}

public static partial class NativeBattleModulePackets
{
    /// <summary>572BE0's G3 emission, including native AVSZ3 and zero fourth proxy payload.</summary>
    public static NativeBattleGeometryPrimitive? EmitGouraudTriangle572BE0(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleProjectedVertex> vertices, uint flags, int fade, int tableBaseBucket, byte depthShift, uint depthColor = 0)
    {
        return EmitProjected572200(face, vertices, 4, flags, 0, fade, tableBaseBucket, depthShift, depthColor);
    }
}
