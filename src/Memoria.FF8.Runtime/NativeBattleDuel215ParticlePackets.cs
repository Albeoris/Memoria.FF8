using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel215ParticleObservation(uint Flags, int? Nclip, int? AverageDepth);

public static class NativeBattleDuel215ParticlePackets
{
    public static NativeBattleGfGeometryBatch Emit69CE00(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, int tableBaseBucket, byte depthShift, int byteOffset = 0, Action<NativeBattleDuel215ParticleObservation>? observe = null)
    {
        if (face.Length != 20 || depthShift > 31)
        {
            throw new InvalidDataException("Native Duel215 debris requires its actual20-byte G3 record and depth shift.");
        }
        NativeBattleModuleProjectedVertex[] vertices = [readCorner(0), readCorner(1), readCorner(2)];
        SkinWrites packet = new(28, byteOffset);
        packet.Word(0, 0x06000000);
        packet.Word(4, BinaryPrimitives.ReadUInt32LittleEndian(face));
        uint flags = vertices[0].Flags | vertices[1].Flags | vertices[2].Flags;
        int? nclip = null;
        int? depth = null;
        if ((flags & 0x60000) != 0)
        {
            return Rejected();
        }
        for (int corner = 0; corner < 3; corner++)
        {
            packet.Word(8 + corner * 8, vertices[corner].Screen);
        }
        packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(face[12..]));
        packet.Word(20, BinaryPrimitives.ReadUInt32LittleEndian(face[16..]));
        nclip = NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen);
        if (nclip < 0)
        {
            return Rejected();
        }
        if (vertices.All(vertex => unchecked((ushort)vertex.Screen) > 2560) || vertices.All(vertex => unchecked((ushort)(vertex.Screen >> 16)) > 1728))
        {
            return Rejected();
        }
        int average = (int)(unchecked((uint)vertices.Sum(vertex => (int)vertex.Depth) * 0x155u) >> 12);
        depth = average;
        observe?.Invoke(new(flags, nclip, depth));
        NativeBattleGeometryPrimitive primitive = new(checked(tableBaseBucket + (average >> depthShift)), packet.Words(), [0, 0, 0, 0]);
        return new([packet.Attempt(primitive)], 28);

        NativeBattleGfGeometryBatch Rejected()
        {
            observe?.Invoke(new(flags, nclip, depth));
            return new([packet.Attempt(null)], 0);
        }
    }
}
