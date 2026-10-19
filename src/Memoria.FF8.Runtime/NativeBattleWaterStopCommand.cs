using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>The selected stop/remove operation over the actual current slot and shared order owners.</summary>
public static class NativeBattleWaterStopCommand
{
    public static NativeBattleWaterExecutionFrame WaitStop1D(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, short, uint> resolveSlot)
    {
        short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        uint selected = resolveSlot(frame, selector);
        uint manager = NativeBattleWaterExecutionFields.CoreDword(frame, 0x27973EC);
        uint first = NativeBattleWaterExecutionFields.CoreDword(frame, unchecked(manager + 0x90));
        return selected == first ? StopRemoveAA5120(frame) : frame.WriteTemporaryWord(0x13E, 0x80);
    }

    public static NativeBattleWaterExecutionFrame StopRemoveAA5120(NativeBattleWaterExecutionFrame frame)
    {
        if (frame.ReadTemporaryByte(0x143) != 0)
        {
            frame = NativeBattleWaterExecutionFields.WriteCoreDword(frame, 0x2797450, 0);
            return frame.WriteTemporaryWord(0x13E, 0x8000);
        }
        int current = NativeBattleWaterExecutionFields.SlotOffset(frame, 0, 16);
        for (int offset = 0; offset < 16; offset += 4)
        {
            frame = frame with { Arena = frame.Arena.Write(current + offset, new byte[4]) };
        }
        byte index = frame.ReadTemporaryByte(0x142);
        int masked = index & 0x7F;
        int active = Find(0x2797454, masked, false);
        if (active < 0)
        {
            throw new NotSupportedException("Native slot removal requires its requested physical slot within the bounded active-order owner.");
        }
        Shift(0x2797454, active);
        uint manager = NativeBattleWaterExecutionFields.CoreDword(frame, 0x27973EC);
        uint bank = NativeBattleWaterExecutionFields.CoreDword(frame, checked(manager + 0x90));
        uint idAddress = unchecked(bank + (uint)(0x100 * masked) + 0x12);
        ushort activeId = BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(idAddress, 2), 2));
        ClearId(activeId, true);
        frame = RemoveDrawB65270(frame, index);
        uint header = NativeBattleWaterExecutionFields.CoreDword(frame, 0x279744C);
        ushort count = NativeBattleWaterExecutionFields.CoreWord(frame, checked(header + 0x18));
        frame = NativeBattleWaterExecutionFields.WriteCoreWord(frame, checked(header + 0x18), unchecked((ushort)(count - 1)));
        frame = frame.WriteTemporaryWord(0x13E, NativeBattleWaterExecutionFields.SlotWord(frame, 0xC8));
        frame = NativeBattleWaterExecutionFields.WriteCoreDword(frame, 0x2797450, 0);
        frame = frame.WriteTemporaryByte(0x144, unchecked((byte)(frame.ReadTemporaryByte(0x144) - 1)));
        ClearId(NativeBattleWaterExecutionFields.SlotWord(frame, 0x12), false);
        return frame;

        int Find(uint address, int wanted, bool stopAtTerminator)
        {
            for (int cursor = 0; cursor < 128; cursor++)
            {
                byte entry = frame.Core.Read(address + (uint)cursor, 1)[0];
                if (stopAtTerminator && entry == 0xFF)
                {
                    return -1;
                }
                if ((entry & 0x7F) == wanted)
                {
                    return cursor;
                }
            }
            return -1;
        }

        void Shift(uint address, int cursor)
        {
            for (; cursor < 127; cursor++)
            {
                byte next = frame.Core.Read(address + (uint)cursor + 1, 1)[0];
                frame = frame with { Core = frame.Core.Write(address + (uint)cursor, new byte[] { next }) };
                if (next == 0xFF)
                {
                    return;
                }
            }
            throw new NotSupportedException("Native slot removal exhausted its bounded order owner before the shifted terminator.");
        }

        void ClearId(ushort id, bool signedTable)
        {
            for (int cursor = 0; cursor < 64; cursor++)
            {
                uint address = 0x2797204 + (uint)(2 * cursor);
                ushort raw = NativeBattleWaterExecutionFields.CoreWord(frame, address);
                int candidate = signedTable ? unchecked((short)raw) : raw;
                if (candidate == id)
                {
                    frame = NativeBattleWaterExecutionFields.WriteCoreWord(frame, address, 0);
                    return;
                }
            }
        }
    }

    public static NativeBattleWaterExecutionFrame RemoveDrawB65270(NativeBattleWaterExecutionFrame frame, byte index)
    {
        for (int cursor = 0; cursor < 128; cursor++)
        {
            byte entry = frame.Core.Read(0x2797554 + (uint)cursor, 1)[0];
            if (entry == 0xFF)
            {
                return frame;
            }
            if ((entry & 0x7F) != index)
            {
                continue;
            }
            for (; cursor < 127; cursor++)
            {
                byte next = frame.Core.Read(0x2797554 + (uint)cursor + 1, 1)[0];
                frame = frame with { Core = frame.Core.Write(0x2797554 + (uint)cursor, new byte[] { next }) };
                if (next == 0xFF)
                {
                    return frame;
                }
            }
            throw new NotSupportedException("Native draw removal exhausted its bounded order owner before the shifted terminator.");
        }
        throw new NotSupportedException("Native draw removal left its bounded order owner without a match or terminator.");
    }

}
