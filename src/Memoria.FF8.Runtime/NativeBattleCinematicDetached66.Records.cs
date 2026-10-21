using System.Buffers.Binary;
using System.Collections.Immutable;
using Memory = Memoria.FF8.Runtime.NativeBattleApocalypse221BreakupMemory;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicDetached66Motion(NativeBattleWaterExecutionFrame Frame, NativeBattleActionMatrix Matrix);

public static partial class NativeBattleCinematicDetached66
{
    public static NativeBattleWaterExecutionFrame InitializeRecord(NativeBattleWaterExecutionFrame frame, uint address, ImmutableArray<NativeBattleVector> vertices, NativeBattleVector origin, bool quad)
    {
        Memory memory = new(frame);
        InitializeRecord(memory, address, vertices, origin, quad);
        return memory.Frame;
    }

    private static void InitializeRecord(Memory memory, uint address, ImmutableArray<NativeBattleVector> vertices, NativeBattleVector origin, bool quad)
    {
        if (vertices.Length != (quad ? 4 : 3))
        {
            throw new ArgumentException("Detached polygon initialization requires the actual corner count.", nameof(vertices));
        }
        int[] center = [vertices.Sum(vertex => (int)vertex.X) / vertices.Length, vertices.Sum(vertex => (int)vertex.Y) / vertices.Length, vertices.Sum(vertex => (int)vertex.Z) / vertices.Length];
        for (int axis = 0; axis < 3; axis++)
        {
            memory.Word(address + (uint)(0x24 + axis * 2), center[axis]);
            memory.Dword(address + (uint)(axis * 4), unchecked((uint)(center[axis] << 8)));
            if (quad)
            {
                memory.Word(address + (uint)(0x18 + axis * 2), 0);
            }
        }
        int dx = center[0] - origin.X;
        int dy = center[1] - origin.Y;
        int dz = center[2] - origin.Z;
        int xy = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(dx * dx + dy * dy));
        int distance = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(xy * xy + dz * dz));
        memory.Word(address + 0x2A, distance);
    }

    private static void PrepareScalars(Memory memory, uint payload)
    {
        int[] sources = [0xE, 0x16, 0x1E, 0x10, 0x18, 0x20, 0x12, 0x1A, 0x22, 0x14, 0x1C, 0x24, 0x26, 0x2A, 0x2E, 0x28, 0x2C, 0x30];
        int[] destinations = [0xA0, 0xA2, 0xA4, 0xA8, 0xAA, 0xAC, 0xB0, 0xB2, 0xB4, 0xB8, 0xBA, 0xBC, 0xC0, 0xC2, 0xC4, 0xC8, 0xCA, 0xCC];
        int multiplier = memory.Signed(memory.Slot + 0x96);
        for (int index = 0; index < sources.Length; index++)
        {
            memory.Word(memory.Temporary + (uint)destinations[index], memory.Signed(payload + (uint)sources[index]) * multiplier / 256);
        }
    }

    public static NativeBattleWaterExecutionFrame ActivateRecord(NativeBattleWaterExecutionFrame frame, uint address, uint payload)
    {
        Memory memory = new(frame);
        PrepareScalars(memory, payload);
        ActivateRecord(memory, address, payload);
        return memory.Frame;
    }

    private static void ActivateRecord(Memory memory, uint address, uint payload)
    {
        if (unchecked((sbyte)memory.Byte(address + 0x2C)) >= 0 || memory.Signed(address + 0x2A) >= memory.Signed(memory.Slot + 0x94))
        {
            return;
        }
        int[] ranges = [0xA8, 0xB8, 0xAA, 0xBA, 0xAC, 0xBC, 0xC8, 0xCA, 0xCC];
        int[] bases = [0xA0, 0xB0, 0xA2, 0xB2, 0xA4, 0xB4, 0xC0, 0xC2, 0xC4];
        int[] destinations = [0xC, 0xE, 0x10, 0x12, 0x14, 0x16, 0x1E, 0x20, 0x22];
        for (int index = 0; index < ranges.Length; index++)
        {
            int value = memory.Random(memory.Signed(memory.Temporary + (uint)ranges[index]));
            memory.Word(address + (uint)destinations[index], value + memory.Signed(memory.Temporary + (uint)bases[index]));
        }
        for (int axis = 0; axis < 3; axis++)
        {
            uint field = address + (uint)(axis * 4);
            int product = unchecked((int)memory.Dword(field) * memory.Signed(memory.Slot + (uint)(0x8C + axis * 2)));
            memory.Dword(field, unchecked((uint)(product / 256)));
        }
        memory.Byte(address + 0x2C, memory.Byte(payload + 6));
    }

    public static NativeBattleCinematicDetached66Motion MoveRecord(NativeBattleWaterExecutionFrame frame, uint address, NativeBattleActionMatrix basis)
    {
        Memory memory = new(frame);
        NativeBattleActionMatrix matrix = MoveRecord(memory, address, basis);
        return new(memory.Frame, matrix);
    }

    private static NativeBattleActionMatrix MoveRecord(Memory memory, uint address, NativeBattleActionMatrix basis)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            uint velocity = address + (uint)(0xC + axis * 4);
            memory.Word(velocity, memory.Signed(velocity) + memory.Signed(velocity + 2));
            uint position = address + (uint)(axis * 4);
            memory.Dword(position, unchecked(memory.Dword(position) + (uint)(memory.Signed(velocity) << 4)));
            uint angle = address + (uint)(0x18 + axis * 2);
            memory.Word(angle, memory.Word(angle) + memory.Word(angle + 6));
        }
        NativeBattleVector positionVector = new(unchecked((short)((int)memory.Dword(address) >> 8)), unchecked((short)((int)memory.Dword(address + 4) >> 8)), unchecked((short)((int)memory.Dword(address + 8) >> 8)));
        NativeBattleFullMacVertex translation = NativeBattleGfMatrices.TransformMac460840(basis, positionVector);
        NativeBattleVector angles = new(unchecked((short)(memory.Word(address + 0x18) << 4)), unchecked((short)(memory.Word(address + 0x1A) << 4)), unchecked((short)(memory.Word(address + 0x1C) << 4)));
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Multiply56C090(NativeBattleGfMatrices.Euler56CD50(angles), basis);
        rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, memory.Signed(memory.Slot + 0x8C) << 4, memory.Signed(memory.Slot + 0x8E) << 4, memory.Signed(memory.Slot + 0x90) << 4);
        for (int axis = 0; axis < 3; axis++)
        {
            memory.Dword(memory.Temporary + (uint)(0xC0 + axis * 4), unchecked((uint)(memory.Signed(memory.Slot + (uint)(0x8C + axis * 2)) << 4)));
        }
        for (int index = 0; index < 9; index++)
        {
            memory.Word(memory.Temporary + (uint)(0xD0 + index * 2), rotation.Rotation[index]);
        }
        bool[] known = memory.Frame.TemporaryFrame.Known.ToArray();
        known[0xE2] = false;
        known[0xE3] = false;
        memory.Frame = memory.Frame with { TemporaryFrame = memory.Frame.TemporaryFrame with { Known = known.ToImmutableArray() } };
        return rotation with { X = translation.X, Y = translation.Y, Z = translation.Z };
    }

    private static NativeBattleGfGeometryBatch Packet(Memory memory, Face face, ImmutableArray<NativeBattleModuleProjectedVertex> projected, uint color)
    {
        int corners = face.Quad ? 4 : 3;
        if (projected.Length != corners)
        {
            throw new NotSupportedException("Detached polygon projection must retain the actual face corner count.");
        }
        int length = face.Quad ? 40 : 32;
        byte[] bytes = new byte[length];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
        bytes[3] = face.Quad ? (byte)9 : (byte)7;
        mask[3] = NativeBattleGfPacketByteState.Known;
        Dword(4, color);
        for (int corner = 0; corner < corners; corner++)
        {
            Dword(8 + corner * 8, projected[corner].Screen);
            Word(12 + corner * 8, memory.Word(face.Address + (uint)(4 + corner * 2)));
        }
        Word(14, memory.Word(face.Address + (face.Quad ? 20u : 16u)));
        Word(22, (ushort)(memory.Word(face.Address + (face.Quad ? 22u : 18u)) | (memory.Byte(memory.Slot + 0x92) & 0x60)));
        ImmutableArray<NativeBattleGfPacketByteState> writes = mask.ToImmutableArray();
        ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(0, words, []) { PhysicalWriteMask = writes };
        NativeBattleGfGeometryAttempt attempt = new(0, bytes.ToImmutableArray(), writes, primitive);
        return new([attempt], length);

        void Word(int offset, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 2);
        }

        void Dword(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
        }
    }
}
