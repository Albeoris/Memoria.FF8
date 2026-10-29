using System.Buffers.Binary;
using System.Collections.Immutable;
using Memory = Memoria.FF8.Runtime.NativeBattleApocalypse221BreakupMemory;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicBreakupAuxiliaryProjection(NativeBattleWaterExecutionFrame Frame, uint Screen, int QuarterDepth, uint Flags)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public System.Numerics.Vector3? PilotWorld { get; init; }
}
public sealed record NativeBattleCinematicBreakupAuxiliaryServices(Func<NativeBattleWaterExecutionFrame, NativeBattleVector, NativeBattleCinematicBreakupAuxiliaryProjection> Project, Func<NativeBattleWaterExecutionFrame, ImmutableArray<int>, NativeBattleWaterExecutionFrame> PublishPayload4, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

public static class NativeBattleCinematicBreakupAuxiliary
{
    public const int RecordSize = 28;

    public static NativeBattleWaterExecutionFrame Initialize(NativeBattleWaterExecutionFrame frame, uint state, uint payload)
    {
        Memory memory = new(frame);
        RequireMode(memory, payload);
        int capacity = memory.Word(payload + 0x2E);
        if (capacity == 0)
        {
            return frame;
        }
        memory.Word(state + 0x1C, 0);
        uint pool = memory.Allocate(checked((uint)(capacity * RecordSize)));
        memory.Dword(memory.Slot + 0xA0, pool);
        for (int index = 0; index < capacity; index++)
        {
            memory.Byte(pool + (uint)(index * RecordSize + 24), 0);
        }
        return memory.Frame;
    }

    public static NativeBattleWaterExecutionFrame Emit(NativeBattleWaterExecutionFrame frame, uint fragment)
    {
        Memory memory = new(frame);
        return Emit(frame, unchecked((int)memory.Dword(fragment + 4)), unchecked((int)memory.Dword(fragment + 8)), unchecked((int)memory.Dword(fragment + 12)));
    }

    public static NativeBattleWaterExecutionFrame Emit(NativeBattleWaterExecutionFrame frame, int fixedX, int fixedY, int fixedZ)
    {
        Memory memory = new(frame);
        uint state = frame.ReadTemporaryDword(0x84);
        uint pool = frame.ReadTemporaryDword(0x8C);
        uint payload = frame.ReadTemporaryDword(0x90);
        RequireMode(memory, payload);
        int old = memory.Word(state + 0x1C);
        memory.Word(state + 0x1C, old + 1);
        int index = Memory.Divide(old, memory.Word(payload + 0x2E));
        int remainder = old - index * memory.Word(payload + 0x2E);
        uint row = checked(pool + (uint)(remainder * RecordSize));
        NativeBattlePilotVisualLifetimes.Allocated(memory.Frame.Arena, pool, remainder);
        memory.Byte(row + 24, 1);
        memory.Byte(row + 25, memory.Byte(payload + 0x32));
        memory.Dword(row, unchecked((uint)fixedX));
        memory.Dword(row + 4, unchecked((uint)fixedY));
        memory.Dword(row + 8, unchecked((uint)fixedZ));
        for (int indexInRow = 0; indexInRow < 6; indexInRow++)
        {
            uint source = payload + (uint)(0x34 + indexInRow * 4);
            int value = memory.Random(memory.Signed(source + 2)) + memory.Signed(source);
            memory.Word(row + (uint)(12 + indexInRow * 2), value);
        }
        return memory.Frame;
    }

    public static NativeBattleWaterExecutionFrame AdvanceDraw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicBreakupAuxiliaryServices services, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        Memory memory = new(frame);
        uint state = frame.ReadTemporaryDword(0x84);
        uint pool = frame.ReadTemporaryDword(0x8C);
        uint payload = frame.ReadTemporaryDword(0x90);
        RequireMode(memory, payload);
        int capacity = memory.Word(payload + 0x2E);
        uint output = memory.Dword(memory.Manager + 0xD8);
        for (int index = 0; index < capacity; index++)
        {
            uint row = pool + (uint)(index * RecordSize);
            if (memory.Byte(row + 24) == 0)
            {
                continue;
            }
            byte lifetime = unchecked((byte)(memory.Byte(row + 25) - 1));
            memory.Byte(row + 25, lifetime);
            if (unchecked((sbyte)lifetime) < 0)
            {
                memory.Byte(row + 24, 0);
                continue;
            }
            for (int axis = 0; axis < 3; axis++)
            {
                uint velocity = row + (uint)(12 + axis * 4);
                memory.Word(velocity, memory.Signed(velocity) + memory.Signed(velocity + 2));
                uint position = row + (uint)(axis * 4);
                memory.Dword(position, unchecked(memory.Dword(position) + (uint)(memory.Signed(velocity) << 4)));
            }
            if (memory.Word(payload + 8) == 1 && unchecked((int)memory.Dword(row + 4)) > 0)
            {
                memory.Word(row + 0x10, 0);
                memory.Word(row + 0x12, 0);
            }
            NativeBattleVector positionVector = new(unchecked((short)((int)memory.Dword(row) >> 8)), unchecked((short)((int)memory.Dword(row + 4) >> 8)), unchecked((short)((int)memory.Dword(row + 8) >> 8)));
            if (memory.Word(payload + 8) == 6)
            {
                int radius = unchecked((int)memory.Dword(row)) >> 8;
                int angle = unchecked((int)memory.Dword(row + 8)) >> 4;
                positionVector = new(unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12)), positionVector.Y, unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12)));
            }
            memory.Frame = memory.Frame.WriteTemporaryWord(0xF0, unchecked((ushort)positionVector.X)).WriteTemporaryWord(0xF2, unchecked((ushort)positionVector.Y)).WriteTemporaryWord(0xF4, unchecked((ushort)positionVector.Z));
            uint color = memory.Dword(unchecked(memory.Dword(state + 0xC) + (uint)(unchecked((sbyte)lifetime) * 4))) | 0x6A000000;
            NativeBattleCinematicBreakupAuxiliaryProjection projected = services.Project(memory.Frame, positionVector);
            memory.Frame = projected.Frame.WriteTemporaryDword(0xFC, projected.Flags);
            int depth = unchecked(projected.QuarterDepth << 2);
            memory.Frame = services.PublishPayload4(memory.Frame, [depth, depth, depth, depth]);
            int bucket = projected.QuarterDepth >> 2;
            byte[] bytes = new byte[12];
            bytes[3] = 2;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), color);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), projected.Screen);
            ImmutableArray<NativeBattleGfPacketByteState> mask = [NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched, .. Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 9)];
            NativeBattleGeometryPrimitive? primitive = bucket < 0xFFF ? new(0, [0x02000000, color, projected.Screen], [depth, depth, depth, depth]) { PhysicalWriteMask = mask } : null;
            if (primitive is not null && pilotIdentity is { } identity && projected.PilotWorld is { } anchor)
            {
                NativeBattlePilotVisualIdentity particleIdentity = NativeBattlePilotVisualLifetimes.Identity(identity.Effect, memory.Frame.Arena, pool, index);
                primitive = primitive with { PilotGeometry = new(particleIdentity, anchor, anchor, anchor, anchor, 4) { ScreenOffsets = [new(0, 0), new(8, 0), new(0, 8), new(8, 8)], DepthQuantum = 4 } };
            }
            NativeBattleGfGeometryAttempt attempt = new(0, bytes.ToImmutableArray(), mask, primitive);
            NativeBattleGfGeometryBatch batch = new([attempt], 12) { NativeUnlinkedAllocation = primitive is null };
            uint ordering = unchecked(memory.Frame.ReadTemporaryDword(0x14C) + (uint)(bucket * 4));
            memory.Frame = services.Append(memory.Frame, output, ordering, batch);
            output = unchecked(output + 12);
        }
        memory.Dword(memory.Manager + 0xD8, output);
        return memory.Frame;
    }

    private static void RequireMode(Memory memory, uint payload)
    {
        if (memory.Word(payload + 8) is not (1 or 2 or 5 or 6 or 7))
        {
            throw new NotSupportedException("Native breakup auxiliary particles require their supported current mode.");
        }
    }
}
