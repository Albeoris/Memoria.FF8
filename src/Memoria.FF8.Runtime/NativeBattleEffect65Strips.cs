using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect65Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Strips
{
    public static NativeBattleEffect65Frame Initialize6E3910(NativeBattleEffect65Frame frame, int shift, int jitter, int depth)
    {
        int magnitude = 1 << (shift & 31);
        frame = frame with { State = frame.State.WriteWord(0x2544FE8, unchecked((ushort)(15 - shift))).WriteWord(0x2544FEA, unchecked((ushort)(magnitude / 2))).WriteWord(0x2544FEC, unchecked((ushort)jitter)).WriteWord(0x2544FEE, unchecked((ushort)depth)) };
        uint common = frame.State.ReadDword(ArenaGlobal);
        for (int index = 0; index < 256; index++)
        {
            frame = WriteBytes(frame, checked(common + 0x3B536u + (uint)(index * 24)), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(ushort.MaxValue)), 2);
        }
        for (int index = 0; index < 32; index++)
        {
            frame = WriteBytes(frame, checked(common + 0x3CD30u + (uint)(index * 56)), NativeBattleEffectArena.Clear(), 4);
        }
        return frame with { State = frame.State.WriteDword(0x2544FBC, common + 0x3B530).WriteDword(0x2544FC0, common + 0x3CD30) };
    }

    public static (NativeBattleEffect65Frame Frame, int Value) Random6E3F10(NativeBattleEffect65Frame frame)
    {
        uint value = unchecked(frame.State.ReadDword(RandomGlobal) * 125 + 14) & 0x7FFF;
        return (frame with { State = frame.State.WriteDword(RandomGlobal, value) }, checked((int)value));
    }

    public static (NativeBattleEffect65Frame Frame, uint Address) AllocatePoint6E41D0(NativeBattleEffect65Frame frame, bool capturePresentation = false)
    {
        uint start = checked(frame.State.ReadDword(ArenaGlobal) + 0x3B530);
        uint address = frame.State.ReadDword(0x2544FBC);
        bool Free(uint point) => Word(ReadBytes(frame, point + 6, 2), 0) == -1;
        if (address < start || address >= start + 256 * 24 || (address - start) % 24 != 0)
        {
            throw new InvalidDataException("Effect65 strip point cursor leaves its current physical pool.");
        }
        if (!Free(address))
        {
            address = start;
            int remaining = 256;
            while (!Free(address))
            {
                address += 24;
                if (--remaining == 0)
                {
                    return (frame, 0);
                }
            }
        }
        if (capturePresentation)
        {
            NativeBattlePilotVisualLifetimes.Allocated(frame.Memory.Arena!, start, checked((int)((address - start) / 24)));
        }
        uint next = address >= start + 255 * 24 ? start : address + 24;
        return (frame with { State = frame.State.WriteDword(0x2544FBC, next) }, address);
    }

    public static (NativeBattleEffect65Frame Frame, uint Address) Birth6E4F60(NativeBattleEffect65Frame frame, NativeBattleEffectArena descriptor56, bool capturePresentation = false)
    {
        uint common = frame.State.ReadDword(ArenaGlobal);
        uint start = common + 0x3CD30;
        uint address = frame.State.ReadDword(0x2544FC0);
        bool Free(uint record) => Dword(ReadBytes(frame, record, 4), 0) == 0;
        if (address < start || address >= start + 32 * 56 || (address - start) % 56 != 0)
        {
            throw new InvalidDataException("Effect65 strip record cursor leaves its current physical pool.");
        }
        if (!Free(address))
        {
            address = start;
            int remaining = 32;
            while (!Free(address))
            {
                address += 56;
                if (--remaining == 0)
                {
                    return (frame, 0);
                }
            }
        }
        frame = frame with { State = frame.State.WriteDword(0x2544FC0, address >= start + 31 * 56 ? start : address + 56) };
        (frame, uint pointAddress) = AllocatePoint6E41D0(frame, capturePresentation);
        if (pointAddress == 0)
        {
            return (frame, 0);
        }
        NativeBattleEffectArena point = ReadBytes(frame, pointAddress, 24).Write(0x14, BitConverter.GetBytes(0u)).Write(8, new byte[8]);
        point = NativeBattleEffectNodeContextMemory.Copy(descriptor56, 0x10, point, 0, 8);
        point = NativeBattleEffectNodeContextMemory.Copy(descriptor56, 0x30, point, 0x10, 4);
        frame = WriteBytes(frame, pointAddress, point, 24);
        NativeBattleEffectArena record = ReadBytes(frame, address, 56).Write(0, BitConverter.GetBytes(pointAddress)).Write(4, BitConverter.GetBytes(pointAddress));
        record = NativeBattleEffectNodeContextMemory.Copy(descriptor56, 8, record, 8, 40);
        record = NativeBattleEffectNodeContextMemory.Copy(descriptor56, 0x30, record, 0x30, 4);
        record = PutWord(record, 0x34, 1);
        record = NativeBattleEffectNodeContextMemory.Copy(descriptor56, 0x36, record, 0x36, 2);
        if (capturePresentation)
        {
            NativeBattlePilotVisualLifetimes.Allocated(frame.Memory.Arena!, start, checked((int)((address - start) / 56)));
        }
        return (WriteBytes(frame, address, record, 56), address);
    }

    public static NativeBattleEffect65Frame Birth6E4DA0(NativeBattleEffect65Frame frame, NativeBattleEffectArena center8, short angle, int phase, int scale, int firstRadius, int secondRadius, uint firstPointAddress, uint secondPointAddress, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        int sum = unchecked(firstRadius + secondRadius);
        if (scale <= sum / 2)
        {
            return frame;
        }
        (frame, int draw) = Random6E3F10(frame);
        if (unchecked(draw * scale) / 16384 >= sum)
        {
            return frame;
        }
        NativeBattleVector first = Circle(angle, unchecked(scale + firstRadius));
        NativeBattleVector second = Circle(unchecked(angle + phase), unchecked(scale + secondRadius));
        NativeBattleEffectArena descriptor = NativeBattleEffectArena.Clear().MarkUnknown(0, 56).Write(8, BitConverter.GetBytes(firstPointAddress)).Write(12, BitConverter.GetBytes(secondPointAddress));
        descriptor = Point(descriptor, 0x10, first).Write(0x16, BitConverter.GetBytes((short)10000));
        descriptor = Point(descriptor, 0x18, second).Write(0x1E, new byte[2]);
        for (int index = 0; index < 3; index++)
        {
            (frame, draw) = Random6E3F10(frame);
            descriptor = PutWord(descriptor, 0x20 + index * 2, (unchecked(draw * 1000) >> 15) - 500);
        }
        descriptor = PutWord(descriptor, 0x26, 512);
        for (int index = 0; index < 3; index++)
        {
            (frame, draw) = Random6E3F10(frame);
            descriptor = PutWord(descriptor, 0x28 + index * 2, (unchecked(draw * 1000) >> 15) - 500);
        }
        descriptor = PutWord(PutWord(PutWord(PutWord(descriptor, 0x2E, 0), 0x30, 12288), 0x32, -512), 0x36, 4096);
        return Birth6E4F60(frame, descriptor, inputs.CapturePresentation).Frame;

        NativeBattleVector Circle(int orientation, int radius) => new(unchecked((short)(Word(center8, 0) + (unchecked(unchecked((short)NativeBattleCameraMath.Sine(orientation)) * radius) >> 12))), Word(center8, 2), unchecked((short)(Word(center8, 4) + (unchecked(unchecked((short)NativeBattleCameraMath.Cosine(orientation)) * radius) >> 12))));
    }

    public static NativeBattleEffect65Frame Advance6E3F30(NativeBattleEffect65Frame frame, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        uint start = checked(frame.State.ReadDword(ArenaGlobal) + 0x3CD30);
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        for (int index = 0; index < 32; index++)
        {
            uint address = checked(start + (uint)(index * 56));
            NativeBattleEffectArena record = ReadBytes(frame, address, 56);
            uint head = Dword(record, 0);
            if (head == 0)
            {
                continue;
            }
            record = PutWord(record, 0x36, Word(record, 0x36) - 256);
            if (Word(record, 0x36) <= 0)
            {
                frame = frame with { State = frame.State.WriteDword(0x2544FBC, head).WriteDword(0x2544FC0, address) };
                for (uint point = head; point != 0; point = Dword(ReadBytes(frame, point + 0x14, 4), 0))
                {
                    frame = WriteBytes(frame, point + 6, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(ushort.MaxValue)), 2);
                }
                frame = WriteBytes(frame, address, record.Write(0, BitConverter.GetBytes(0u)), 56);
                continue;
            }
            if (Word(record, 0x1E) < 4096)
            {
                record = PutWord(record, 0x1E, Word(record, 0x1E) + Word(record, 0x26));
                for (int axis = 0; axis < 3; axis++)
                {
                    record = PutWord(record, 0x10 + axis * 2, Word(record, 0x10 + axis * 2) + Word(record, 0x20 + axis * 2));
                }
                int shift = frame.State.ReadWord(0x2544FE8) & 31;
                short magnitude = unchecked((short)frame.State.ReadWord(0x2544FEA));
                for (int axis = 0; axis < 3; axis++)
                {
                    (frame, int sample) = Random6E3F10(frame);
                    record = PutWord(record, 0x20 + axis * 2, Word(record, 0x20 + axis * 2) + (sample >> shift) - magnitude);
                }
                (frame, int threshold) = Random6E3F10(frame);
                if (threshold < unchecked((short)frame.State.ReadWord(0x2544FEC)))
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        (frame, int sample) = Random6E3F10(frame);
                        record = PutWord(record, 0x10 + axis * 2, Word(record, 0x10 + axis * 2) + (sample >> ((shift - 2) & 31)) - unchecked((short)(magnitude << 2)));
                    }
                }
                short fraction = Word(record, 0x1E);
                NativeBattleEffectArena output;
                if (fraction >= 4096)
                {
                    record = PutWord(record, 0x1E, 4096);
                    fraction = 4096;
                    output = NativeBattleEffectNodeContextMemory.Copy(record, 0x18, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                }
                else
                {
                    registers = Weight(registers, Vector(record, 0x10), 4096 - fraction, false, inputs.Colors);
                    registers = Weight(registers, Vector(record, 0x18), fraction, true, inputs.Colors);
                    output = Point(NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, Ir(registers));
                }
                (frame, uint pointAddress) = AllocatePoint6E41D0(frame, inputs.CapturePresentation);
                if (pointAddress == 0)
                {
                    frame = WriteBytes(frame, address, PutWord(record, 0x1E, 4096), 56);
                    return Publish(frame, registers);
                }
                NativeBattleEffectArena point = NativeBattleEffectNodeContextMemory.Copy(output, 0, ReadBytes(frame, pointAddress, 24), 0, 8).Write(0x14, BitConverter.GetBytes(0u));
                point = PutWord(point, 6, Word(record, 0x16));
                point = NativeBattleEffectNodeContextMemory.Copy(record, 0x30, point, 0x10, 4);
                uint tail = Dword(record, 4);
                frame = WriteBytes(frame, tail + 0x14, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(pointAddress)), 4);
                record = record.Write(4, BitConverter.GetBytes(pointAddress));
                int factorAddress = (fraction & ~1) * 2;
                short factor = BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(checked(TrigonometryAddress + (uint)factorAddress), 2).AsSpan());
                registers = Weight(registers, Vector(record, 0x28), factor, false, inputs.Colors);
                point = Point(point, 8, Ir(registers));
                point = PutWord(point, 0xE, fraction);
                frame = WriteBytes(frame, pointAddress, point, 24);
                record = PutWord(record, 0x34, Word(record, 0x34) + 1);
            }
            for (uint pointAddress = Dword(record, 0); pointAddress != 0;)
            {
                NativeBattleEffectArena point = ReadBytes(frame, pointAddress, 24);
                point = PutWord(point, 0x10, Math.Max(0, (int)unchecked((short)(Word(point, 0x10) + Word(point, 0x12)))));
                for (int axis = 0; axis < 3; axis++)
                {
                    point = PutWord(point, axis * 2, Word(point, axis * 2) + Word(point, 8 + axis * 2));
                }
                frame = WriteBytes(frame, pointAddress, point, 24);
                pointAddress = Dword(point, 0x14);
            }
            head = Dword(record, 0);
            if (Word(ReadBytes(frame, head + 0x10, 2), 0) <= 1024)
            {
                uint next = Dword(ReadBytes(frame, head + 0x14, 4), 0);
                if (Word(ReadBytes(frame, next + 0x10, 2), 0) <= 1024)
                {
                    while (true)
                    {
                        frame = frame with { State = frame.State.WriteDword(0x2544FBC, head) };
                        frame = WriteBytes(frame, head + 6, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(ushort.MaxValue)), 2);
                        head = next;
                        record = PutWord(record.Write(0, BitConverter.GetBytes(head)), 0x34, Word(record, 0x34) - 1);
                        if (Word(record, 0x34) < 2)
                        {
                            record = record.Write(0, BitConverter.GetBytes(0u));
                            frame = frame with { State = frame.State.WriteDword(0x2544FC0, address) };
                            break;
                        }
                        if (Word(ReadBytes(frame, head + 0x10, 2), 0) > 1024)
                        {
                            break;
                        }
                        next = Dword(ReadBytes(frame, head + 0x14, 4), 0);
                        if (Word(ReadBytes(frame, next + 0x10, 2), 0) > 1024)
                        {
                            break;
                        }
                    }
                }
            }
            frame = WriteBytes(frame, address, record, 56);
        }
        return Publish(frame, registers);
    }

    private static NativeBattleGeometryRegisterStep Weight(NativeBattleGeometryRegisterStep registers, NativeBattleVector value, int factor, bool accumulate, NativeBattleSummonColors colors)
    {
        registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } }, unchecked((ushort)value.X), unchecked((ushort)value.Y), unchecked((ushort)value.Z));
        return NativeBattleRaldo19BallRendering.Weighted(registers, accumulate, colors);
    }
    private static NativeBattleVector Ir(NativeBattleGeometryRegisterStep registers) => new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
    private static NativeBattleVector Vector(NativeBattleEffectArena row, int offset) => new(Word(row, offset), Word(row, offset + 2), Word(row, offset + 4));
    private static short Word(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadInt16LittleEndian(row.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(row.Read(offset, 4));
    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena row, int offset, int value) => row.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    private static NativeBattleEffectArena Point(NativeBattleEffectArena row, int offset, NativeBattleVector point) => PutWord(PutWord(PutWord(row, offset, point.X), offset + 2, point.Y), offset + 4, point.Z);
}
