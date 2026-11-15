using System.Buffers.Binary;
using NodeKey = Memoria.FF8.Runtime.BattleExecutionReference.CinematicNodeKey;

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
        uint active = FindOrderEntry(frame.Core, 0x2797454, masked, stopAtTerminator: false) ?? throw new InvalidDataException("Native active-order search cannot stop on FF.");
        ShiftOrder(frame.Core, active);
        uint manager = NativeBattleWaterExecutionFields.CoreDword(frame, 0x27973EC);
        uint bank = NativeBattleWaterExecutionFields.CoreDword(frame, checked(manager + 0x90));
        uint idAddress = unchecked(bank + (uint)(0x100 * masked) + 0x12);
        NodeKey activeId = NativeBattleCinematicNodeKeys.Read(frame.Arena, NativeBattleWaterExecutionFields.ArenaOffset(idAddress, 2));
        ClearId(activeId, true);
        frame = RemoveDrawB65270(frame, index);
        uint header = NativeBattleWaterExecutionFields.CoreDword(frame, 0x279744C);
        ushort count = NativeBattleWaterExecutionFields.CoreWord(frame, checked(header + 0x18));
        frame = NativeBattleWaterExecutionFields.WriteCoreWord(frame, checked(header + 0x18), unchecked((ushort)(count - 1)));
        frame = frame.WriteTemporaryWord(0x13E, NativeBattleWaterExecutionFields.SlotWord(frame, 0xC8));
        frame = NativeBattleWaterExecutionFields.WriteCoreDword(frame, 0x2797450, 0);
        frame = frame.WriteTemporaryByte(0x144, unchecked((byte)(frame.ReadTemporaryByte(0x144) - 1)));
        ClearId(NativeBattleCinematicNodeKeys.Read(frame.Arena, NativeBattleWaterExecutionFields.SlotOffset(frame, 0x12, 2)), false);
        return frame;

        void ClearId(NodeKey id, bool signedTable)
        {
            for (int cursor = 0; cursor < 64; cursor++)
            {
                uint address = 0x2797204 + (uint)(2 * cursor);
                NodeKey stored = frame.Core.ReadCinematicNodeKey(address);
                bool matches = signedTable && stored is NodeKey.Literal raw && id is NodeKey.Literal literal
                    ? unchecked((short)raw.Value) == literal.Value
                    : stored == id;
                if (matches)
                {
                    frame = NativeBattleWaterExecutionFields.WriteCoreWord(frame, address, 0);
                    return;
                }
            }
        }
    }

    public static NativeBattleWaterExecutionFrame RemoveDrawB65270(NativeBattleWaterExecutionFrame frame, byte index)
    {
        if (FindOrderEntry(frame.Core, 0x2797554, index, stopAtTerminator: true) is uint address)
        {
            ShiftOrder(frame.Core, address);
        }
        return frame;
    }

    private const uint CoreEnd = NativeBattleScriptedEffectMemory.Address + NativeBattleScriptedEffectMemory.Length;

    private static uint? FindOrderEntry(NativeBattleScriptedEffectMemory core, uint start, int wanted, bool stopAtTerminator)
    {
        for (uint address = start; address < CoreEnd; address++)
        {
            byte entry = core.Read(address, 1)[0];
            if (stopAtTerminator && entry == 0xFF)
            {
                return null;
            }
            if ((entry & 0x7F) == wanted)
            {
                return address;
            }
        }
        throw new NotSupportedException("Native order removal exhausted its scripted core before finding the requested index or a usable terminator.");
    }

    private static void ShiftOrder(NativeBattleScriptedEffectMemory core, uint destination)
    {
        uint source = destination + 1;
        // Native compaction follows the actual FF byte, which can lie beyond the nominal order table.
        for (uint address = source; address < CoreEnd; address++)
        {
            if (core.Read(address, 1)[0] == 0xFF)
            {
                byte[] shifted = core.Read(source, checked((int)(address - source + 1)));
                core.Write(destination, shifted);
                return;
            }
        }
        throw new NotSupportedException("Native order removal exhausted its scripted core before the shifted terminator.");
    }
}
