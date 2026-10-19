using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel274MeshPackets
{
    public static NativeBattleGfGeometryBatch EmitBatch68BD50(ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, int depthBias, uint renderFlags, Func<int, NativeBattleVector> readSelectedVertex)
    {
        return NativeBattleEffectMeshPackets.EmitBatch(mesh, matrix, projection, 0x2000, readSelectedVertex, (group, face, corner, offset) => group is 4 or 5 ? EmitGouraud68BDB0(face, corner, group == 5, depthBias, (renderFlags & 0x20) != 0, offset) : new([], 0));
    }

    public static NativeBattleGfGeometryBatch EmitBatch68C320(ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, ushort verticalOffset, Func<int, NativeBattleVector> readSelectedVertex)
    {
        return NativeBattleEffectMeshPackets.EmitBatch(mesh, matrix, projection, 0x2000, readSelectedVertex, (group, face, corner, offset) => group == 2 ? EmitFt3_68C350(face, corner, verticalOffset, offset) : new([], 0));
    }

    public static NativeBattleGfGeometryBatch EmitFt3_68C350(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, ushort verticalOffset, int byteOffset = 0)
    {
        NativeBattleEffectMeshWindow window = new(0, verticalOffset, new(0, 0, 256, 256), new(0, 0, 256, 128), true, 128);
        NativeBattleEffectMeshPacketContext context = new(0, null, null, null, null, 0, false, null, 17, 2, window);
        return NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, false, false, context, null, byteOffset);
    }

    public static NativeBattleGfGeometryBatch EmitGouraud68BDB0(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, bool quad, int depthBias, bool allowBackface, int byteOffset = 0)
    {
        if (face.Length != (quad ? 24 : 20))
        {
            throw new InvalidDataException("Native Duel Gouraud leaf requires its actual G3/G4 source record.");
        }
        NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
        for (int corner = 0; corner < 3; corner++)
        {
            vertices[corner] = readCorner(corner);
        }
        int length = quad ? 36 : 28;
        SkinWrites packet = new(length, byteOffset);
        packet.Word(0, (uint)(length / 4 - 1) << 24);
        uint command = BinaryPrimitives.ReadUInt32LittleEndian(face);
        packet.Word(4, command);
        if (((vertices[0].Flags | vertices[1].Flags | vertices[2].Flags) & 0x60000) != 0)
        {
            return Rejected();
        }
        if (NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen) < 0 && !allowBackface)
        {
            return Rejected();
        }
        for (int corner = 0; corner < 3; corner++)
        {
            packet.Word(8 + corner * 8, vertices[corner].Screen);
        }
        if (quad)
        {
            vertices[3] = readCorner(3);
            packet.Word(32, vertices[3].Screen);
        }
        if (vertices.All(vertex => unchecked((ushort)vertex.Screen) > 2560) || vertices.All(vertex => unchecked((ushort)(vertex.Screen >> 16)) > 1728))
        {
            return Rejected();
        }
        for (int corner = 1; corner < vertices.Length; corner++)
        {
            packet.Word(4 + corner * 8, BinaryPrimitives.ReadUInt32LittleEndian(face[(8 + corner * 4)..]));
        }
        int average = (int)(unchecked((uint)vertices.Sum(vertex => (int)vertex.Depth) * (quad ? 0x100u : 0x155u)) >> 12);
        int biased = unchecked(average + depthBias);
        if (biased < 0)
        {
            return Rejected();
        }
        int bucket = checked(17 + (biased >> 2));
        NativeBattleGeometryPrimitive primitive = new(bucket, packet.Words(), NativeBattleEffectMeshPackets.Payload(vertices, command));
        SkinWrites linkOnly = new(length, byteOffset);
        NativeBattleGfGeometryAttempt before = Mode(length, 0xE1000020);
        NativeBattleGfGeometryAttempt after = Mode(length + 8, 0xE1000040);
        return new([packet.Attempt(null), before, linkOnly.Attempt(primitive), after], length + 16);

        NativeBattleGfGeometryBatch Rejected() => new([packet.Attempt(null)], 0);
        NativeBattleGfGeometryAttempt Mode(int offset, uint value)
        {
            SkinWrites mode = new(8, checked(byteOffset + offset));
            mode.Word(0, 0x01000000);
            mode.Word(4, value);
            return mode.Attempt(new(bucket, mode.Words(), [0, 0, 0, 0]));
        }
    }
}
