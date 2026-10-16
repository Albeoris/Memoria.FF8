using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>570650's original571240 mesh, matrix and572200 context.</summary>
public static class NativeBattleDefensivePackets
{
    public static NativeBattleModulePacketBatch Emit570650(NativeBattleModuleDraw draw, NativeBattleDefensiveResource resource, NativeBattleCameraProjection projection)
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
        return NativeBattleModulePackets.EmitMesh572200(mesh, new(checked((byte)(8 + resource.Variant)), resource.Address, resource.Bytes), projection, 17, 2);
    }
}

public static partial class NativeBattleModulePackets
{
    /// <summary>572BE0's G3 emission, including native AVSZ3 and zero fourth proxy payload.</summary>
    public static NativeBattleGeometryPrimitive? EmitGouraudTriangle572BE0(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleProjectedVertex> vertices, uint flags, int fade, int tableBaseBucket, byte depthShift)
    {
        if (face.Length != 20 || vertices.Count != 3 || depthShift > 31)
        {
            throw new InvalidDataException("Native572BE0 requires its original20-byte face, three projected vertices and depth shift.");
        }
        if (vertices.Any(vertex => (vertex.DepthAndClip & 0x60000) != 0))
        {
            return null;
        }
        if (Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen) < 0 && (flags & 0x20) == 0)
        {
            return null;
        }
        if (vertices.All(vertex => unchecked((ushort)vertex.Screen) > 0xA00) || vertices.All(vertex => (vertex.Screen >> 16) > 0x6C0))
        {
            return null;
        }
        uint first = BinaryPrimitives.ReadUInt32LittleEndian(face);
        if ((flags & 2) != 0)
        {
            first |= 0x2000000;
        }
        if ((flags & 8) != 0)
        {
            first &= 0xFDFFFFFF;
        }
        uint[] words = new uint[7];
        words[0] = 0x6000000;
        for (int corner = 0; corner < 3; corner++)
        {
            uint color = corner == 0 ? first : BinaryPrimitives.ReadUInt32LittleEndian(face[(8 + corner * 4)..]);
            if ((flags & 0x80) != 0)
            {
                color = Fade(color, fade);
                if (corner != 0)
                {
                    //572BE0 loads FIFO colors1/2/0;45E120 takes the modified first packet color as RGBC.
                    color = (color & 0xFFFFFF) | (first & 0xFF000000);
                }
            }
            words[1 + corner * 2] = color;
            words[2 + corner * 2] = vertices[corner].Screen;
        }
        //56BAA0 writes ZSF3=0x155.45E5C0 multiplies the three unsigned depths, then SHR12.
        uint depth = unchecked((uint)vertices.Sum(vertex => (int)vertex.Depth) * 0x155) >> 12;
        int bucket = checked(tableBaseBucket + ((int)depth >> depthShift));
        return new(bucket, words.ToImmutableArray(), [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, 0]);
    }
}
