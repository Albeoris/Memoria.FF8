using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicStageShatter49Capture(int OriginX, int OriginY, int OriginZ, int RadiusSquared, short AngularBase, short AngularRange, short VerticalBase, short VerticalRange);
public sealed record NativeBattleCinematicStageShatter49Emission(NativeBattleWaterExecutionFrame Frame, NativeBattleGfGeometryBatch Packets, uint OrderingAddress);

public static partial class NativeBattleCinematicStageShatter49
{
    public const int RecordSize = 44;
    public const int Capacity = 1024;
    public const int AllocationBytes = RecordSize * Capacity;

    public static NativeBattleWaterExecutionFrame InitializeRecords(NativeBattleWaterExecutionFrame frame, uint address, short lifetime)
    {
        _ = ArenaOffset(address, AllocationBytes);
        for (int index = 0; index < Capacity; index++)
        {
            frame = StoreWord(frame, address + (uint)(index * RecordSize + 6), unchecked((ushort)(lifetime << 8)));
        }
        return frame;
    }

    public static NativeBattleWaterExecutionFrame CaptureFace(NativeBattleWaterExecutionFrame frame, uint recordAddress, ImmutableArray<NativeBattleVector> vertices, NativeBattleCinematicStageShatter49Capture inputs)
    {
        if (vertices.IsDefault || vertices.Length is not (3 or 4))
        {
            throw new InvalidDataException("Native stage shatter requires its original triangle or quad vertices.");
        }
        if (Read(frame, recordAddress + 6, 1)[0] != 0)
        {
            return frame;
        }
        int x = vertices.Sum(vertex => (int)vertex.X) / vertices.Length - inputs.OriginX;
        int y = vertices.Sum(vertex => (int)vertex.Y) / vertices.Length - inputs.OriginY;
        int z = vertices.Sum(vertex => (int)vertex.Z) / vertices.Length - inputs.OriginZ;
        int radius = unchecked(x * x + y * y + z * z);
        if (radius >= inputs.RadiusSquared)
        {
            return frame;
        }
        frame = StoreVector(frame, recordAddress, new(unchecked((short)x), unchecked((short)y), unchecked((short)z)));
        frame = StoreByte(frame, recordAddress + 6, 1);
        frame = StoreDword(frame, recordAddress + 8, 0);
        for (int corner = 0; corner < vertices.Length; corner++)
        {
            NativeBattleVector vertex = vertices[corner];
            NativeBattleVector local = new(unchecked((short)(vertex.X - x)), unchecked((short)(vertex.Y - y)), unchecked((short)(vertex.Z - z)));
            frame = StoreVector(frame, recordAddress + (uint)(12 + corner * 8), local);
        }
        for (int axis = 0; axis < 3; axis++)
        {
            NativeBattleWaterVectorRandom sampled = NativeBattleWaterVectorCommands.RandomB65740(inputs.AngularRange, frame.Random);
            frame = frame with { Random = sampled.Random };
            uint angle = recordAddress + (uint)(0x12 + axis * 8);
            frame = StoreByte(frame, angle, 0);
            frame = StoreByte(frame, angle + 1, unchecked((byte)(sampled.Value + inputs.AngularBase)));
        }
        NativeBattleWaterVectorRandom vertical = NativeBattleWaterVectorCommands.RandomB65740(inputs.VerticalRange, frame.Random);
        frame = frame with { Random = vertical.Random };
        return StoreByte(frame, recordAddress + 9, unchecked((byte)((inputs.VerticalBase + vertical.Value) >> 4)));
    }

    public static NativeBattleWaterExecutionFrame AdvanceFragment(NativeBattleWaterExecutionFrame frame, uint recordAddress, bool frozen)
    {
        if (frozen)
        {
            return frame;
        }
        frame = StoreByte(frame, recordAddress + 7, unchecked((byte)(Read(frame, recordAddress + 7, 1)[0] - 1)));
        int y = SignedWord(frame, recordAddress + 2) + (unchecked((sbyte)Read(frame, recordAddress + 9, 1)[0]) << 4);
        frame = StoreWord(frame, recordAddress + 2, unchecked((ushort)y));
        for (int axis = 0; axis < 3; axis++)
        {
            uint angle = recordAddress + (uint)(0x12 + axis * 8);
            frame = StoreByte(frame, angle, unchecked((byte)(Read(frame, angle, 1)[0] + Read(frame, angle + 1, 1)[0])));
        }
        return frame;
    }

    public static NativeBattleCinematicStageShatter49Emission RenderFace(NativeBattleWaterExecutionFrame frame, uint recordAddress, NativeBattleRawPolygon polygon, ImmutableArray<NativeBattleModuleProjectedVertex> originalVertices, NativeBattleActionMatrix camera, NativeBattleCameraProjection projection, uint materialTable)
    {
        int corners = polygon.Quad ? 4 : 3;
        int length = polygon.Quad ? 40 : 32;
        if (polygon.Bytes.IsDefault || polygon.Bytes.Length != (polygon.Quad ? 24 : 20) || originalVertices.IsDefault || originalVertices.Length != corners)
        {
            throw new InvalidDataException("Native stage shatter requires its complete colored stage face and current projected corners.");
        }
        byte[] data = new byte[length];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
        data[3] = polygon.Quad ? (byte)9 : (byte)7;
        mask[3] = NativeBattleGfPacketByteState.Known;
        int[] uvOffsets = polygon.Quad ? [8, 12, 16, 18] : [8, 12, 6];
        for (int corner = 0; corner < corners; corner++)
        {
            PacketWord(12 + corner * 8, FaceWord(uvOffsets[corner]));
        }
        PacketWord(14, FaceWord(10));
        PacketWord(22, FaceWord(14));
        bool active = Read(frame, recordAddress + 6, 1)[0] != 0;
        int lifetime = unchecked((sbyte)Read(frame, recordAddress + 7, 1)[0]);
        NativeBattleModuleProjectedVertex[] projected = originalVertices.ToArray();
        bool emitted = false;
        uint ordering = frame.ReadTemporaryDword(0x14C);
        if (!active)
        {
            PacketDword(4, BinaryPrimitives.ReadUInt32LittleEndian(polygon.Bytes.AsSpan().Slice(polygon.Quad ? 20 : 16)));
            emitted = NativeBattleModulePackets.Nclip(projected[0].Screen, projected[1].Screen, projected[2].Screen) > 0;
            if (emitted)
            {
                int sum = projected.Take(3).Sum(vertex => (int)vertex.Depth);
                if (polygon.Quad)
                {
                    sum += unchecked((short)projected[3].Depth);
                }
                int average = sum / corners;
                ordering = unchecked(ordering + (uint)((average >> 2) & ~3));
            }
        }
        else if (lifetime >= 0)
        {
            uint color = BinaryPrimitives.ReadUInt32LittleEndian(Read(frame, unchecked(materialTable + (uint)(lifetime * 4)), 4));
            PacketDword(4, color | (polygon.Quad ? 0x2E000000u : 0x26000000u));
            PacketWord(22, (ushort)(FaceWord(14) | 0x20));
            frame = AdvanceFragment(frame, recordAddress, frame.ReadTemporaryByte(0x145) != 0);
            NativeBattleVector angles = new(unchecked((short)(unchecked((sbyte)Read(frame, recordAddress + 0x12, 1)[0]) << 4)), unchecked((short)(unchecked((sbyte)Read(frame, recordAddress + 0x1A, 1)[0]) << 4)), unchecked((short)(unchecked((sbyte)Read(frame, recordAddress + 0x22, 1)[0]) << 4)));
            NativeBattleVector center = Vector(frame, recordAddress);
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CD50(angles) with { X = center.X, Y = center.Y, Z = center.Z };
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(camera, local);
            if (polygon.Quad)
            {
                projected[3] = NativeBattleModuleProjection.Project45FAA0(matrix, Vector(frame, recordAddress + 36), projection, 0, 0);
            }
            for (int corner = 0; corner < 3; corner++)
            {
                projected[corner] = NativeBattleModuleProjection.Project45FE10(matrix, Vector(frame, recordAddress + (uint)(12 + corner * 8)), projection);
            }
            ushort depth = projected[2].Depth;
            frame = frame.WriteTemporaryWord(0xFC, depth);
            ordering = unchecked(ordering + (uint)((depth >> 2) & ~3));
            emitted = true;
        }
        if (emitted)
        {
            for (int corner = 0; corner < corners; corner++)
            {
                PacketDword(8 + corner * 8, projected[corner].Screen);
            }
        }
        ImmutableArray<NativeBattleGfPacketByteState> physicalMask = mask.ToImmutableArray();
        ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
        NativeBattleGeometryPrimitive? primitive = emitted ? new(0, words, []) { PhysicalWriteMask = physicalMask } : null;
        NativeBattleGfGeometryAttempt attempt = new(0, data.ToImmutableArray(), physicalMask, primitive);
        return new(frame, new([attempt], emitted ? length : 0), ordering);

        ushort FaceWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(offset));

        void PacketWord(int offset, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 2);
        }

        void PacketDword(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
        }
    }

    private static byte[] Read(NativeBattleWaterExecutionFrame frame, uint address, int length) => frame.Arena.Read(ArenaOffset(address, length), length);
    private static short SignedWord(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(Read(frame, address, 2));
    private static NativeBattleVector Vector(NativeBattleWaterExecutionFrame frame, uint address) => new(SignedWord(frame, address), SignedWord(frame, address + 2), SignedWord(frame, address + 4));

    private static NativeBattleWaterExecutionFrame StoreByte(NativeBattleWaterExecutionFrame frame, uint address, byte value)
    {
        return frame with { Arena = frame.Arena.Write(ArenaOffset(address, 1), new byte[] { value }) };
    }

    private static NativeBattleWaterExecutionFrame StoreWord(NativeBattleWaterExecutionFrame frame, uint address, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), bytes) };
    }

    private static NativeBattleWaterExecutionFrame StoreDword(NativeBattleWaterExecutionFrame frame, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), bytes) };
    }

    private static NativeBattleWaterExecutionFrame StoreVector(NativeBattleWaterExecutionFrame frame, uint address, NativeBattleVector value)
    {
        frame = StoreWord(frame, address, unchecked((ushort)value.X));
        frame = StoreWord(frame, address + 2, unchecked((ushort)value.Y));
        return StoreWord(frame, address + 4, unchecked((ushort)value.Z));
    }
}
