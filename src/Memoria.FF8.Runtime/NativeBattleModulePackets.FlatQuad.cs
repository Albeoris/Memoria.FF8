using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleModulePackets
{
    /// <summary>572500's original twelve-byte F4 records and24-byte GPU packet.</summary>
    public static NativeBattleGeometryPrimitive? EmitFlatQuad572500(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleProjectedVertex> vertices, uint flags, int fade, int tableBaseBucket, byte depthShift, uint depthColor = 0)
    {
        if (face.Length != 12 || vertices.Count != 4 || depthShift > 31)
        {
            throw new InvalidDataException("Native572500 requires its original F4 record and four projected vertices.");
        }
        if (((vertices[0].DepthAndClip | vertices[1].DepthAndClip | vertices[2].DepthAndClip) & 0x60000) != 0)
        {
            return null;
        }
        if (Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen) < 0 && (flags & 0x10) == 0)
        {
            return null;
        }
        if (vertices.All(vertex => unchecked((ushort)vertex.Screen) > 0xA00) || vertices.All(vertex => (vertex.Screen >> 16) > 0x6C0))
        {
            return null;
        }
        uint command = BinaryPrimitives.ReadUInt32LittleEndian(face);
        if ((flags & 1) != 0)
        {
            command |= 0x02000000;
        }
        if ((flags & 4) != 0)
        {
            command &= 0xFDFFFFFF;
        }
        if ((flags & 0x40) != 0)
        {
            command = Fade(command, fade, depthColor);
        }
        return new(Bucket(vertices, tableBaseBucket, depthShift), [0x05000000, command, vertices[0].Screen, vertices[1].Screen, vertices[2].Screen, vertices[3].Screen], vertices.Select(vertex => (int)vertex.Depth).ToImmutableArray());
    }
}
