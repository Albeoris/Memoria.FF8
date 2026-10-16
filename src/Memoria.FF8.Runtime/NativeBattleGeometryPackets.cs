using System.Buffers.Binary;
using System.Collections.Immutable;
namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGeometryPacket(int Bucket, NativeBattlePrimitivePacket Packet, ImmutableArray<int> VertexPayload4);
public sealed record NativeBattleGeometryPrimitive(int Bucket, ImmutableArray<uint> Words, ImmutableArray<int> VertexPayload4);

/// <summary>50FDF0's original FT3/FT4 packet emission; inputs are original records, never float-renderer depth estimates.</summary>
public static class NativeBattleGeometryPackets
{
    public static NativeBattleGeometryPacket? Emit50FDF0(ReadOnlySpan<byte> polygon, IReadOnlyList<NativeBattleProjectedVertex> vertices, bool quad, uint colorFlags28, int tableBaseBucket, byte depthShift, uint packetAddress, short? colorDepth36 = null, NativeBattleSummonColors? colorRules = null)
    {
        NativeBattleGeometryPrimitive? primitive = EmitRaw50FDF0(polygon, vertices, quad, colorFlags28, tableBaseBucket, depthShift, colorDepth36, colorRules);
        return primitive is null ? null : new(primitive.Bucket, new(packetAddress, primitive.Words), primitive.VertexPayload4);
    }

    public static NativeBattleGeometryPrimitive? EmitRaw50FDF0(ReadOnlySpan<byte> polygon, IReadOnlyList<NativeBattleProjectedVertex> vertices, bool quad, uint colorFlags28, int tableBaseBucket, byte depthShift, short? colorDepth36 = null, NativeBattleSummonColors? colorRules = null)
    {
        int baseLength = quad ? 20 : 16;
        bool perPolygonColor = polygon.Length == baseLength + 4;
        if (polygon.Length != baseLength && !perPolygonColor || depthShift > 31)
        {
            throw new InvalidDataException("Native50FDF0 requires an original16-byte FT3 or20-byte FT4 record and its original depth shift.");
        }
        ushort first = BinaryPrimitives.ReadUInt16LittleEndian(polygon);
        int bias = (first >> 12) - 8;
        int count = quad ? 4 : 3;
        NativeBattleProjectedVertex[] corners = new NativeBattleProjectedVertex[count];
        uint anyClip = 0;
        uint sharedClip = uint.MaxValue;
        int minDepth = ushort.MaxValue;
        for (int corner = 0; corner < count; corner++)
        {
            int index = corner == 0 ? first & 0xFFF : BinaryPrimitives.ReadUInt16LittleEndian(polygon[(corner * 2)..]);
            if ((uint)index >= vertices.Count)
            {
                throw new InvalidDataException("Native50FDF0 polygon references a missing original projected vertex.");
            }
            NativeBattleProjectedVertex vertex = vertices[index];
            corners[corner] = vertex;
            anyClip |= vertex.DepthAndClip;
            sharedClip &= vertex.DepthAndClip;
            minDepth = Math.Min(minDepth, vertex.Depth);
        }
        if ((anyClip & 0x60000) != 0 || (sharedClip & 0x3F00000) != 0 || !Accept510680(corners[0].Screen, corners[1].Screen, corners[2].Screen))
        {
            return null;
        }
        uint uv1Page = BinaryPrimitives.ReadUInt32LittleEndian(polygon[12..]);
        uint command;
        if (perPolygonColor)
        {
            short cue = colorDepth36 ?? throw new NotSupportedException("Native50FDF0 colored groups require their actual context WORD36 depth-cue owner.");
            command = BinaryPrimitives.ReadUInt32LittleEndian(polygon[baseLength..]);
            if (cue != 0)
            {
                //5099D0 overwrites RFC/GFC/BFC from contextRGB28 before every mesh;50FDF0 writes RGB6 and the signedIR0 WORD36.
                //The quad's additional08000000 write to IR0 has no effect on45F270's signed lowWORD read.
                command = (colorRules ?? throw new NotSupportedException("Native50FDF0 colored depth cue requires its authenticated45F270 interpolation constants.")).Interpolate(command, cue, colorFlags28);
            }
        }
        else
        {
            command = (colorFlags28 & 0x2FFFFFF) | (quad ? 0x2C000000u : 0x24000000u);
            if ((uv1Page & 0xFE000000) != 0)
            {
                command |= 0x2000000;
            }
        }
        uint[] words = new uint[quad ? 10 : 8];
        words[0] = (uint)(words.Length - 1) << 24;
        words[1] = command;
        words[2] = corners[0].Screen;
        words[3] = BinaryPrimitives.ReadUInt32LittleEndian(polygon[8..]);
        words[4] = corners[1].Screen;
        words[5] = uv1Page;
        words[6] = corners[2].Screen;
        words[7] = BinaryPrimitives.ReadUInt16LittleEndian(polygon[(quad ? 16 : 6)..]);
        if (quad)
        {
            words[8] = corners[3].Screen;
            words[9] = BinaryPrimitives.ReadUInt16LittleEndian(polygon[18..]);
        }
        ImmutableArray<int> depths = corners.Select(vertex => bias + vertex.Depth).ToImmutableArray();
        if (!quad)
        {
            depths = depths.Add(0);
        }
        return new(checked(tableBaseBucket + (minDepth >> depthShift)), words.ToImmutableArray(), depths);
    }

    public static bool Accept510680(uint a, uint b, uint c)
    {
        int ax = unchecked((short)a);
        int bx = unchecked((short)b);
        int cx = unchecked((short)c);
        int ay = unchecked((int)a) >> 16;
        int by = unchecked((int)b) >> 16;
        int cy = unchecked((int)c) >> 16;
        return unchecked((by - cy) * (bx - ax) - (by - ay) * (bx - cx)) < 0;
    }
}
