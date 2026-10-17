using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleModulePackets
{
    /// <summary>572500's original twelve-byte F4 records and24-byte GPU packet.</summary>
    public static NativeBattleGeometryPrimitive? EmitFlatQuad572500(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleProjectedVertex> vertices, uint flags, int fade, int tableBaseBucket, byte depthShift, uint depthColor = 0)
    {
        return EmitProjected572200(face, vertices, 1, flags, 0, fade, tableBaseBucket, depthShift, depthColor);
    }
}
