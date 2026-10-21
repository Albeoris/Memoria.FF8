using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicActorDissolve52Vertex(uint Screen, ushort Depth, byte Clipped);
public sealed record NativeBattleCinematicActorDissolve52Deformation(NativeBattleVector Vertex, byte Clipped, int? RadiusIndex, int? Angle);
public sealed record NativeBattleCinematicActorDissolve52Emission(byte Counter, NativeBattleGfGeometryBatch Packets, uint OrderingAddress);

/// <summary>Current radial deformation and counter-driven actor polygon stores, independent of skeletal and arena ownership.</summary>
public static class NativeBattleCinematicActorDissolve52
{
    public static ImmutableArray<int> BuildRadiusTable(short boundary, short amplitude, short velocity, short acceleration, short angle, short angleStep)
    {
        int rows = 1 - boundary;
        if (rows <= 0)
        {
            return [];
        }
        ImmutableArray<int>.Builder result = ImmutableArray.CreateBuilder<int>(rows);
        int currentAmplitude = amplitude << 12;
        int currentVelocity = velocity << 4;
        int currentAngle = angle;
        for (int row = 0; row < rows; row++)
        {
            result.Add(unchecked((NativeBattleCameraMath.Sine(currentAngle) >> 4) * currentAmplitude) >> 8);
            currentVelocity = unchecked(currentVelocity + acceleration);
            currentAmplitude = unchecked(currentAmplitude + currentVelocity);
            currentAngle = unchecked(currentAngle + angleStep);
        }
        return result.MoveToImmutable();
    }

    public static NativeBattleCinematicActorDissolve52Deformation DeformVertex(NativeBattleVector vertex, short boundary, short clipHeight, Func<int, int> readRadius, Func<short, short, int> direction)
    {
        byte clipped = vertex.Y > clipHeight ? (byte)1 : (byte)0;
        short y = vertex.Y;
        if (y > 0)
        {
            y = 0;
        }
        else if (y <= boundary)
        {
            return new(vertex, clipped, null, null);
        }
        int radiusIndex = y - boundary;
        int angle = direction(vertex.X, vertex.Z);
        int radius = readRadius(radiusIndex) >> 4;
        int x = unchecked((NativeBattleCameraMath.Sine(angle) >> 4) * radius) >> 16;
        int z = unchecked((NativeBattleCameraMath.Cosine(angle) >> 4) * radius) >> 16;
        NativeBattleVector deformed = new(unchecked((short)(vertex.X + x)), y, unchecked((short)(vertex.Z + z)));
        return new(deformed, clipped, radiusIndex, angle);
    }

    public static NativeBattleCinematicActorDissolve52Emission EmitFace(ImmutableArray<byte> face, bool quad, byte counter, short initialPaletteIndex, Func<int, NativeBattleCinematicActorDissolve52Vertex> readVertex, Func<int, uint> readPalette, uint orderingBase)
    {
        if (face.IsDefault || face.Length != (quad ? 20 : 16))
        {
            throw new InvalidDataException("Native actor dissolve requires its complete original actor face.");
        }
        if (counter == 0)
        {
            return new(counter, new([], 0), orderingBase);
        }
        int corners = quad ? 4 : 3;
        NativeBattleCinematicActorDissolve52Vertex[] vertices = Enumerable.Range(0, corners).Select(index => readVertex(FaceWord(index * 2) & 0xFFF)).ToArray();
        bool dissolve = unchecked((sbyte)counter) >= 0 || vertices.Any(vertex => vertex.Clipped != 0);
        int length = quad ? 40 : 32;
        byte[] bytes = new byte[length];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
        ushort page = FaceWord(14);
        if (dissolve)
        {
            int selected = unchecked((sbyte)counter) < 0 ? initialPaletteIndex : unchecked((sbyte)counter);
            PacketDword(4, readPalette(selected) | (quad ? 0x2E000000u : 0x26000000u));
            page |= 0x20;
            counter = unchecked((byte)(selected - 1));
        }
        else
        {
            PacketDword(4, quad ? 0x2C808080u : 0x24808080u);
        }
        PacketWord(22, page);
        for (int corner = 0; corner < 3; corner++)
        {
            PacketDword(8 + corner * 8, vertices[corner].Screen);
        }
        bool accepted = NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen) >= 0;
        uint ordering = orderingBase;
        if (accepted)
        {
            if (quad)
            {
                PacketDword(32, vertices[3].Screen);
            }
            bytes[3] = quad ? (byte)9 : (byte)7;
            mask[3] = NativeBattleGfPacketByteState.Known;
            PacketWord(12, FaceWord(8));
            PacketWord(14, FaceWord(10));
            PacketWord(20, FaceWord(12));
            PacketWord(28, FaceWord(quad ? 16 : 6));
            if (quad)
            {
                PacketWord(36, FaceWord(18));
            }
            int sum = vertices.Sum(vertex => (int)vertex.Depth);
            ordering = unchecked(orderingBase + (uint)(((sum / corners) >> 2) & ~3));
        }
        ImmutableArray<NativeBattleGfPacketByteState> physicalMask = mask.ToImmutableArray();
        ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        NativeBattleGeometryPrimitive? primitive = accepted ? new(0, words, []) { PhysicalWriteMask = physicalMask } : null;
        NativeBattleGfGeometryAttempt attempt = new(0, bytes.ToImmutableArray(), physicalMask, primitive);
        return new(counter, new([attempt], accepted ? length : 0), ordering);

        ushort FaceWord(int offset)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan().Slice(offset, 2));
        }

        void PacketWord(int offset, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 2);
        }

        void PacketDword(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
        }
    }
}
