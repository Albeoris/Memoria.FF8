using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicMonsterPackets
{
    public static NativeBattleGfGeometryBatch Glow(NativeBattleRawMesh mesh, IReadOnlyList<NativeBattleProjectedVertex> projected, IReadOnlyList<uint> colors, int tableBaseBucket, IReadOnlyList<bool>? colorHighKnown = null, IReadOnlyList<System.Numerics.Vector3>? pilotWorldVertices = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        mesh.Validate();
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int faceIndex = -1;
        foreach (NativeBattleRawPolygon face in mesh.Polygons)
        {
            faceIndex++;
            int plain = face.Quad ? 20 : 16;
            if (face.Bytes.Length != plain)
            {
                continue;
            }
            int count = face.Quad ? 4 : 3;
            int[] indices = Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(index * 2)) & 4095).ToArray();
            if (indices.Any(index => index >= projected.Count || index >= colors.Count))
            {
                throw new InvalidDataException("Native cinematic actor glow face exceeds its actual projected/color vertices.");
            }
            if (NativeBattleModulePackets.Nclip(projected[indices[0]].Screen, projected[indices[1]].Screen, projected[indices[2]].Screen) < 0)
            {
                continue;
            }
            byte[] bytes = new byte[face.Quad ? 52 : 40];
            bool[] known = Enumerable.Repeat(true, bytes.Length).ToArray();
            known[0] = false;
            known[1] = false;
            known[2] = false;
            bytes[3] = face.Quad ? (byte)12 : (byte)9;
            int[] uv = face.Quad ? [8, 12, 16, 18] : [8, 12, 6];
            bool[] unknown = new bool[bytes.Length];
            int[] depths = new int[4];
            int sum = 0;
            for (int corner = 0; corner < count; corner++)
            {
                NativeBattleProjectedVertex vertex = projected[indices[corner]];
                uint color = colors[indices[corner]];
                if (corner == 0)
                {
                    color = (color & 0xFFFFFF) | (face.Quad ? 0x3C000000u : 0x34000000u);
                }
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + corner * 12), color);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8 + corner * 12), vertex.Screen);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12 + corner * 12), BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(uv[corner])));
                if (corner != 0 && colorHighKnown is not null && !colorHighKnown[indices[corner]])
                {
                    unknown[7 + corner * 12] = true;
                }
                depths[corner] = vertex.Depth;
                sum += vertex.Depth;
            }
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(10)));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26), BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(14)));
            known[38] = false;
            known[39] = false;
            if (face.Quad)
            {
                known[50] = false;
                known[51] = false;
            }
            ImmutableArray<uint> words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
            ImmutableArray<NativeBattleGfPacketByteState> mask = known.Select((value, index) => unknown[index] ? NativeBattleGfPacketByteState.Unknown : value ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(checked(tableBaseBucket + ((sum / count >> 2) & ~3) / 4), words, depths.ToImmutableArray()) { PhysicalWriteMask = mask };
            if (pilotIdentity is { } identity && pilotWorldVertices is not null)
            {
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 65537 + faceIndex) }, pilotWorldVertices[indices[0]], pilotWorldVertices[indices[1]], pilotWorldVertices[indices[2]], face.Quad ? pilotWorldVertices[indices[3]] : default, checked((byte)count)) };
            }
            attempts.Add(new(allocated, bytes.ToImmutableArray(), mask, primitive));
            allocated += bytes.Length;
        }
        return new(attempts.ToImmutable(), allocated);
    }

    public static NativeBattleGfGeometryBatch Pack(ImmutableArray<NativeBattleGeometryPrimitive> primitives)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        foreach (NativeBattleGeometryPrimitive primitive in primitives)
        {
            byte[] bytes = new byte[primitive.Words.Length * 4];
            for (int index = 0; index < primitive.Words.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), primitive.Words[index]);
            }
            attempts.Add(new(allocated, bytes.ToImmutableArray(), primitive.PhysicalWriteMask ?? Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray(), primitive));
            allocated += bytes.Length;
        }
        return new(attempts.ToImmutable(), allocated);
    }
}

