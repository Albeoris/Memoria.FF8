using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>5088A0's ten projected vertices, two draw-mode packets and eight-triangle shadow fan.</summary>
public static class NativeBattleShadowPackets
{
    public static ImmutableArray<NativeBattleGeometryPrimitive> Emit5088A0(IReadOnlyList<NativeBattleModuleProjectedVertex> vertices, uint color44, bool gouraudShadows, int tableBaseBucket, byte depthShift)
    {
        if (vertices.Count != 10)
        {
            throw new InvalidDataException("Native5088A0 requires its center and nine perimeter vertices.");
        }
        uint flags = 0;
        foreach (NativeBattleModuleProjectedVertex vertex in vertices)
        {
            flags |= vertex.Flags;
        }
        if ((flags & 0x60000) != 0 || NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen) < 0)
        {
            return [];
        }
        //56C880 returns SZ3>>2;5088A0 stores its lowWORD and uses an x86 byte shift count.
        int bucket = checked(tableBaseBucket + ((vertices[0].Depth >> 2) >> ((depthShift - 2) & 31)));
        uint color = gouraudShadows ? color44 : (color44 & 0xFF000000) | (color44 & 0xFEFEFE) >> 1;
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder packets = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
        packets.Add(new(bucket, [0x01000000, 0xE1000620], [0, 0, 0, 0]));
        int centerDepth = Depth(0);
        for (int triangle = 0; triangle < 8; triangle++)
        {
            int previous = triangle + 1;
            int current = triangle + 2;
            int previousDepth = Depth(previous);
            int currentDepth = Depth(current);
            if (centerDepth <= 0 || previousDepth <= 0 || currentDepth <= 0)
            {
                continue;
            }
            packets.Add(new(bucket, [0x06000000, color, vertices[0].Screen, gouraudShadows ? 0 : color, vertices[previous].Screen, gouraudShadows ? 0 : color, vertices[current].Screen], [centerDepth, previousDepth, currentDepth, 0]));
        }
        packets.Add(new(bucket, [0x01000000, 0xE1000640], [0, 0, 0, 0]));
        return packets.ToImmutable();

        int Depth(int index) => 4 * (vertices[index].Depth >> 2) - 300;
    }
}
