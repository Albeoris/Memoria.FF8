using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect80Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect80Trails
{
    public static NativeBattleEffect80Frame Update7AF9F0(NativeBattleEffect80Frame frame, NativeBattleEffect80Resources resources)
    {
        frame = Bind(frame, resources);
        uint pointer = frame.State.ReadDword(TrailPointerAddress);
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        for (uint index = 0; index < 64; index++)
        {
            uint record = pointer + 0x3000 + index * 48;
            uint head = frame.State.ReadDword(record);
            if (head == 0)
            {
                continue;
            }
            frame = frame with { State = AddWord(frame.State, record + 0x2E, -256) };
            if (SignedWord(frame.State, record + 0x2E) <= 0)
            {
                frame = frame with { State = frame.State.WriteDword(TrailPointCursorGlobal, head) };
                for (uint node = head; node != 0; node = frame.State.ReadDword(node + 0x14))
                {
                    frame = frame with { State = frame.State.WriteWord(node + 6, 0xFFFF) };
                }
                frame = frame with { State = frame.State.WriteDword(record, 0).WriteDword(TrailRecordCursorGlobal, record) };
                continue;
            }
            if (SignedWord(frame.State, record + 0x16) < 4096)
            {
                frame = frame with { State = AddWord(frame.State, record + 0x16, frame.State.ReadWord(record + 0x1E)) };
                for (uint axis = 0; axis < 3; axis++)
                {
                    frame = frame with { State = AddWord(frame.State, record + 8 + axis * 2, frame.State.ReadWord(record + 0x18 + axis * 2)) };
                }
                for (uint axis = 0; axis < 3; axis++)
                {
                    (frame, int random) = Random7AFEA0(frame);
                    int shift = frame.State.ReadWord(0x25DF31C) & 31;
                    frame = frame with { State = AddWord(frame.State, record + 0x18 + axis * 2, (random >> shift) - frame.State.ReadWord(0x25DF31E)) };
                }
                (frame, int chance) = Random7AFEA0(frame);
                if (chance < SignedWord(frame.State, 0x25DF320))
                {
                    for (uint axis = 0; axis < 3; axis++)
                    {
                        (frame, int random) = Random7AFEA0(frame);
                        int shift = (int)(unchecked(frame.State.ReadDword(0x25DF31C) - 2) & 31);
                        int subtraction = unchecked((ushort)(frame.State.ReadWord(0x25DF31E) << 2));
                        frame = frame with { State = AddWord(frame.State, record + 8 + axis * 2, (random >> shift) - subtraction) };
                    }
                }
                short fraction = SignedWord(frame.State, record + 0x16);
                NativeBattleEffectArena position;
                if (fraction >= 4096)
                {
                    frame = frame with { State = frame.State.WriteWord(record + 0x16, 4096) };
                    position = frame.State.ReadView(record + 0x10, 8);
                }
                else
                {
                    position = Interpolate(ref registers, frame.State.ReadView(record + 8, 6), frame.State.ReadView(record + 0x10, 6), fraction);
                }
                (frame, uint point) = AllocatePoint7AFD80(frame);
                if (point == 0)
                {
                    return Publish(frame with { State = frame.State.WriteWord(record + 0x16, 4096) }, registers);
                }
                NativeBattleEffect80State state = frame.State.WriteDword(point + 0x14, 0).Write(point, position.Read(0, 6)).WriteWord(point + 6, frame.State.ReadWord(record + 0xE)).WriteDword(point + 0x10, frame.State.ReadDword(record + 0x28));
                state = state.WriteDword(state.ReadDword(record + 4) + 0x14, point).WriteDword(record + 4, point);
                int shapeIndex = SignedWord(state, record + 0x16) >> 1;
                short factor = BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(unchecked(ShapeTableAddress + (uint)(shapeIndex * 2)), 2).AsSpan());
                registers = WeightVector(registers, state.ReadView(record + 0x20, 6), factor, false);
                state = state.Write(point + 8, IrVector(registers).Read(0, 6));
                state = AddWord(state, record + 0x2C, 1);
                frame = frame with { State = state };
                if (SignedWord(state, record + 0x16) < 0xC00)
                {
                    (frame, int branch) = Random7AFEA0(frame);
                    if (branch < (SignedWord(frame.State, record + 0x26) << 13))
                    {
                        NativeBattleEffectArena descriptor = NativeBattleEffectArena.Clear().MarkUnknown(0, 48);
                        descriptor = NativeBattleEffectNodeContextMemory.Copy(position, 0, descriptor, 8, 6).Write(0xE, BitConverter.GetBytes(frame.State.ReadWord(record + 0xE)));
                        descriptor = NativeBattleEffectNodeContextMemory.Copy(frame.State.ReadView(record + 0x10, 8), 0, descriptor, 0x10, 8);
                        for (uint axis = 0; axis < 3; axis++)
                        {
                            (frame, int random) = Random7AFEA0(frame);
                            descriptor = descriptor.Write(0x18 + (int)axis * 2, BitConverter.GetBytes(unchecked((ushort)(frame.State.ReadWord(record + 0x18 + axis * 2) + (random & 0x1FF) - 0x100))));
                        }
                        descriptor = descriptor.Write(0x1E, BitConverter.GetBytes(frame.State.ReadWord(record + 0x1E))).Write(0x20, frame.State.Read(record + 0x20, 6)).Write(0x26, BitConverter.GetBytes((ushort)0)).Write(0x28, BitConverter.GetBytes(frame.State.ReadDword(record + 0x28))).Write(0x2E, BitConverter.GetBytes(frame.State.ReadWord(record + 0x2E)));
                        (frame, uint child) = Birth7AFDD0(frame, descriptor, resources);
                        if (child != 0)
                        {
                            frame = frame with { State = frame.State.Write(child + 8, frame.State.Read(point + 8, 8)) };
                        }
                        frame = frame with { State = AddWord(frame.State, record + 0x26, -1) };
                    }
                }
            }
            for (uint node = frame.State.ReadDword(record); node != 0; node = frame.State.ReadDword(node + 0x14))
            {
                NativeBattleEffect80State state = AddWord(frame.State, node + 0x10, frame.State.ReadWord(node + 0x12));
                if (SignedWord(state, node + 0x10) < 0)
                {
                    state = state.WriteWord(node + 0x10, 0);
                }
                for (uint axis = 0; axis < 3; axis++)
                {
                    state = AddWord(state, node + axis * 2, state.ReadWord(node + 8 + axis * 2));
                }
                frame = frame with { State = state };
            }
            uint first = frame.State.ReadDword(record);
            while (SignedWord(frame.State, first + 0x10) <= 0x400)
            {
                uint next = frame.State.ReadDword(first + 0x14);
                if (SignedWord(frame.State, next + 0x10) > 0x400)
                {
                    break;
                }
                NativeBattleEffect80State state = AddWord(frame.State, record + 0x2C, -1).WriteDword(TrailPointCursorGlobal, first).WriteWord(first + 6, 0xFFFF).WriteDword(record, next);
                if (SignedWord(state, record + 0x2C) < 2)
                {
                    frame = frame with { State = state.WriteDword(record, 0).WriteDword(TrailRecordCursorGlobal, record) };
                    break;
                }
                frame = frame with { State = state };
                first = next;
            }
        }
        return Publish(frame, registers);
    }

    private static NativeBattleGeometryRegisterStep WeightVector(NativeBattleGeometryRegisterStep registers, NativeBattleEffectArena vector, int factor, bool accumulate)
    {
        ReadOnlySpan<byte> bytes = vector.Read(0, 6);
        registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } }, BinaryPrimitives.ReadUInt16LittleEndian(bytes), BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]));
        return NativeBattleRaldo19BallRendering.Weighted(registers, accumulate, NativeBattleGteColorInterpolation.CodeConstants45F270);
    }
    private static NativeBattleEffectArena IrVector(NativeBattleGeometryRegisterStep registers)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
        for (int axis = 0; axis < 3; axis++)
        {
            result = result.Write(axis * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
        }
        return result;
    }
    private static NativeBattleEffectArena Interpolate(ref NativeBattleGeometryRegisterStep registers, NativeBattleEffectArena a, NativeBattleEffectArena b, int ratio)
    {
        registers = WeightVector(registers, a, 4096 - ratio, false);
        registers = WeightVector(registers, b, ratio, true);
        return IrVector(registers);
    }
}
