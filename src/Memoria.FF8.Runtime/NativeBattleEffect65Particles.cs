using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect65Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Particles
{
    public static NativeBattleEffect65Frame Initialize6E2E20(NativeBattleEffect65Frame frame) => Initialize(frame, 0x3B530, 128, 22, 0x2544FC4, true);
    public static NativeBattleEffect65Frame Initialize6E3390(NativeBattleEffect65Frame frame) => Initialize(frame, 0x3E1F0, 128, 8, 0x2544FCC, false);
    public static NativeBattleEffect65Frame Initialize6E3580(NativeBattleEffect65Frame frame) => Initialize(frame, 0x27690, 64, 32, 0x2544FC8, false);

    private static NativeBattleEffect65Frame Initialize(NativeBattleEffect65Frame frame, int offset, int count, int stride, uint cursor, bool byteLife)
    {
        uint start = checked(frame.State.ReadDword(ArenaGlobal) + (uint)offset);
        for (int index = count - 1; index >= 0; index--)
        {
            frame = WriteBytes(frame, checked(start + (uint)(index * stride + 6)), NativeBattleEffectArena.Clear(), byteLife ? 1 : 2);
        }
        return frame with { State = frame.State.WriteDword(cursor, start) };
    }

    public static (NativeBattleEffect65Frame Frame, uint Address) Allocate6E2E50(NativeBattleEffect65Frame frame)
    {
        (frame, uint address) = AllocateRecord(frame, 0x3B530, 128, 22, 0x2544FC4, true);
        if (address == 0)
        {
            return (frame, 0);
        }
        NativeBattleEffectArena row = ReadBytes(frame, address, 22);
        NativeCrtRandomState random = frame.Random.Draw(out ushort first).Draw(out ushort second).Draw(out ushort third).Draw(out ushort fourth);
        row = row.Write(0x10, BitConverter.GetBytes(first)).Write(0x12, BitConverter.GetBytes(second)).Write(0x14, [unchecked((byte)third), unchecked((byte)fourth)]);
        return (WriteBytes(frame with { Random = random }, address, row, 22), address);
    }

    public static (NativeBattleEffect65Frame Frame, uint Address) Allocate6E34F0(NativeBattleEffect65Frame frame) => AllocateRecord(frame, 0x3E1F0, 128, 8, 0x2544FCC, false);
    public static (NativeBattleEffect65Frame Frame, uint Address) Allocate6E35B0(NativeBattleEffect65Frame frame) => AllocateRecord(frame, 0x27690, 64, 32, 0x2544FC8, false);

    private static (NativeBattleEffect65Frame Frame, uint Address) AllocateRecord(NativeBattleEffect65Frame frame, int offset, int count, int stride, uint cursor, bool byteLife)
    {
        uint start = checked(frame.State.ReadDword(ArenaGlobal) + (uint)offset);
        uint current = frame.State.ReadDword(cursor);
        bool Free(uint address) => byteLife ? ReadBytes(frame, address + 6, 1).Read(0, 1)[0] == 0 : BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(frame, address + 6, 2).Read(0, 2)) == 0;
        if (current < start || (current - start) % stride != 0 || current >= start + count * stride)
        {
            throw new InvalidDataException("Effect65 particle cursor leaves its current physical records.");
        }
        if (!Free(current))
        {
            current = start;
            int remaining = count;
            while (!Free(current))
            {
                current = checked(current + (uint)stride);
                if (--remaining == 0)
                {
                    return (frame, 0);
                }
            }
        }
        uint next = current >= start + (count - 1) * stride ? start : checked(current + (uint)stride);
        return (frame with { State = frame.State.WriteDword(cursor, next) }, current);
    }

    public static NativeBattleEffect65Frame Advance6E3330(NativeBattleEffect65Frame frame)
    {
        uint start = checked(frame.State.ReadDword(ArenaGlobal) + 0x3B530);
        for (int index = 0; index < 128; index++)
        {
            uint address = checked(start + (uint)(index * 22));
            NativeBattleEffectArena row = ReadBytes(frame, address, 22);
            byte life = row.Read(6, 1)[0];
            if (life == 0)
            {
                continue;
            }
            life = unchecked((byte)(life - 1));
            row = row.Write(6, [life]);
            if (life == 0)
            {
                frame = frame with { State = frame.State.WriteDword(0x2544FC4, address) };
            }
            else
            {
                row = PutWord(row, 0x10, Word(row, 0x10) + unchecked((sbyte)row.Read(0x14, 1)[0]));
                row = PutWord(row, 0x12, Word(row, 0x12) + unchecked((sbyte)row.Read(0x15, 1)[0]));
                for (int axis = 0; axis < 3; axis++)
                {
                    row = PutWord(row, axis * 2, Word(row, axis * 2) + Word(row, 8 + axis * 2));
                }
            }
            frame = WriteBytes(frame, address, row, 22);
        }
        return frame;
    }

    public static NativeBattleEffect65Frame Advance6E3550(NativeBattleEffect65Frame frame)
    {
        uint start = checked(frame.State.ReadDword(ArenaGlobal) + 0x3E1F0);
        for (int index = 0; index < 128; index++)
        {
            uint address = checked(start + (uint)(index * 8 + 6));
            ushort life = BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(frame, address, 2).Read(0, 2));
            if (life != 0)
            {
                frame = WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((ushort)(life - 1)))), 2);
            }
        }
        return frame;
    }

    public static NativeBattleEffect65Frame Advance6E38C0(NativeBattleEffect65Frame frame)
    {
        uint start = checked(frame.State.ReadDword(ArenaGlobal) + 0x27690);
        for (int index = 0; index < 64; index++)
        {
            uint address = checked(start + (uint)(index * 32));
            NativeBattleEffectArena row = ReadBytes(frame, address, 32);
            ushort life = unchecked((ushort)Word(row, 6));
            if (life == 0)
            {
                continue;
            }
            row = PutWord(row, 6, --life);
            if (life == 0)
            {
                frame = frame with { State = frame.State.WriteDword(0x2544FC8, address) };
            }
            else
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    row = PutWord(row, axis * 2, Word(row, axis * 2) + Word(row, 0x1A + axis * 2));
                }
            }
            frame = WriteBytes(frame, address, row, 32);
        }
        return frame;
    }

    private static short Word(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadInt16LittleEndian(row.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(row.Read(offset, 4));
    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena row, int offset, int value) => row.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
}
