using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleOilShot136Mesh
{
    public static NativeBattleWindowedMeshStep Draw(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource source, NativeBattleEffectArena context92, NativeBattleCameraProjection projection, int tableBaseBucket = 17, byte depthShift = 2, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        return NativeBattleWindowedMesh.Draw(memory, source, context92, projection, NativeBattleWindowedMeshLayout.OilShot136, ReadVertex, tableBaseBucket, depthShift, pilotIdentity: pilotIdentity);

        (NativeBattleVector Vertex, short? W) ReadVertex(uint address)
        {
            int offset = NativeBattleWaterExecutionFields.ArenaOffset(address, 8);
            NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Oil Shot requires its current selected geometry arena.");
            byte[] xyz = arena.Read(offset, 6);
            (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset + 6, 2);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null);
        }
    }

    public static ImmutableArray<byte> Preprocess6AD430(ImmutableArray<byte> source)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan().Slice(4, 4)));
        if (count < 2 || source.Length < checked(0x1C + (count - 2) * 8 + 2))
        {
            throw new InvalidDataException("Oil Shot requires its complete mutable geometry source.");
        }
        byte[] bytes = source.ToArray();
        int minimum = short.MaxValue;
        int maximum = short.MinValue;
        for (int index = 0; index < count - 1; index++)
        {
            short value = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0x1C + index * 8, 2));
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }
        for (int index = 0; index < count; index++)
        {
            int value = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0xC + index * 8, 2));
            short normalized = unchecked((short)(unchecked((value - minimum) << 12) / (maximum - minimum)));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0xE + index * 8, 2), normalized);
        }
        return bytes.ToImmutableArray();
    }

    public static NativeBattleVector Direction6ADFA0(int first, int second)
    {
        int negativeSine = -NativeBattleCameraMath.Sine(first);
        short x = unchecked((short)(unchecked(NativeBattleCameraMath.Sine(second) * negativeSine) >> 12));
        short y = unchecked((short)(unchecked(-NativeBattleCameraMath.Cosine(second) * negativeSine) >> 12));
        return new(x, y, unchecked((short)NativeBattleCameraMath.Cosine(first)));
    }

    public static NativeBattleOilShot136Frame Deform6ADFF0(NativeBattleOilShot136Frame frame, int factor, NativeBattleOilShot136Resources resources)
    {
        ImmutableArray<byte> source = frame.State.ReadMesh();
        int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan().Slice(4, 4)));
        uint selected = checked(frame.State.ReadDword(NativeBattleOilShot136Profile.WorkingGlobal) + NativeBattleOilShot136Profile.MorphOffset);
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> packed = source.AsSpan().Slice(8 + index * 8, 8);
            NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(packed), BinaryPrimitives.ReadInt16LittleEndian(packed.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(packed.Slice(4)));
            short w = BinaryPrimitives.ReadInt16LittleEndian(packed.Slice(6));
            NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Oil Shot requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Oil Shot requires its current arithmetic registers."));
            registers = NativeBattleGeometry.LoadVector(registers, 0, point, w);
            registers = NativeBattleGeometry.Transform(registers);
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
            int tableIndex = w >> 1;
            if ((uint)tableIndex >= resources.DeformationTable.Length)
            {
                throw new InvalidDataException("Oil Shot deformation requires its actual signed source parameter.");
            }
            short x = unchecked((short)registers.Colors.Ir[0]!.Value);
            short y = unchecked((short)(registers.Colors.Ir[1]!.Value - (unchecked(resources.DeformationTable[tableIndex] * factor) >> 17)));
            short z = unchecked((short)registers.Colors.Ir[2]!.Value);
            byte[] xyz = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(xyz, x);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), y);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), z);
            frame = NativeBattleOilShot136Controller.WriteArena(frame, checked(selected + (uint)(index * 8)), xyz);
        }
        return frame;
    }
}
