using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>5088A0's ten projected vertices, two draw-mode packets and eight-triangle shadow fan.</summary>
public static class NativeBattleShadowPackets
{
    public static ImmutableArray<NativeBattleGeometryPrimitive> Emit5088A0(IReadOnlyList<NativeBattleModuleProjectedVertex> vertices, uint color44, bool gouraudShadows, int tableBaseBucket, byte depthShift)
    {
        return EmitBatch5088A0(vertices, color44, gouraudShadows, tableBaseBucket, depthShift).Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => attempt.Primitive!).ToImmutableArray();
    }

    public static NativeBattleGfGeometryBatch EmitBatch5088A0(IReadOnlyList<NativeBattleModuleProjectedVertex> vertices, uint color44, bool gouraudShadows, int tableBaseBucket, byte depthShift, IReadOnlyList<System.Numerics.Vector3>? pilotWorldVertices = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
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
            return new([], 0);
        }
        //56C880 returns SZ3>>2;5088A0 stores its lowWORD and uses an x86 byte shift count.
        int bucket = checked(tableBaseBucket + ((vertices[0].Depth >> 2) >> ((depthShift - 2) & 31)));
        uint color = gouraudShadows ? color44 : (color44 & 0xFF000000) | (color44 & 0xFEFEFE) >> 1;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        Record([0x01000000, 0xE1000620], [0, 0, 0, 0], true);
        int centerDepth = Depth(0);
        for (int triangle = 0; triangle < 8; triangle++)
        {
            int previous = triangle + 1;
            int current = triangle + 2;
            int previousDepth = Depth(previous);
            int currentDepth = Depth(current);
            Record([0x06000000, color, vertices[0].Screen, gouraudShadows ? 0 : color, vertices[previous].Screen, gouraudShadows ? 0 : color, vertices[current].Screen], [centerDepth, previousDepth, currentDepth, 0], centerDepth > 0 && previousDepth > 0 && currentDepth > 0, triangle, previous, current);
        }
        Record([0x01000000, 0xE1000640], [0, 0, 0, 0], true);
        return new(attempts.ToImmutable(), allocated);

        void Record(ImmutableArray<uint> words, ImmutableArray<int> payload, bool accepted, int triangle = -1, int previous = 0, int current = 0)
        {
            byte[] data = new byte[4 * words.Length];
            for (int index = 0; index < words.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4 * index), words[index]);
            }
            NativeBattleGeometryPrimitive? primitive = accepted ? new(bucket, words, payload) : null;
            if (primitive is not null && triangle >= 0 && pilotIdentity is { } identity && pilotWorldVertices is not null)
            {
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + triangle) }, pilotWorldVertices[0], pilotWorldVertices[previous], pilotWorldVertices[current], default, 3) { DepthQuantum = 4, DepthOffset = -300 } };
            }
            attempts.Add(new(allocated, data.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, data.Length).ToImmutableArray(), primitive));
            if (accepted)
            {
                allocated = checked(allocated + data.Length);
            }
        }

        int Depth(int index) => 4 * (vertices[index].Depth >> 2) - 300;
    }
}
