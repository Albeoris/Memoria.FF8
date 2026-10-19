using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Finite physical-slot creation and secondary instruction-stream attachment.</summary>
public static class NativeBattleWaterChildCommands
{
    public static NativeBattleWaterExecutionFrame Execute5D(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank, uint incomingEcx)
    {
        uint header = NativeBattleWaterExecutionFields.CoreDword(frame, 0x279744C);
        int count = unchecked((sbyte)frame.Core.Read(unchecked(header + 0x41), 1)[0]);
        for (int index = 0; index < count; index++)
        {
            frame = Spawn(frame, program, bank, incomingEcx);
            uint temporary = NativeBattleWaterExecutionFields.CoreDword(frame, 0x2797624);
            header = NativeBattleWaterExecutionFields.CoreDword(frame, 0x279744C);
            uint pointer = frame.ReadTemporaryDword(0xFC);
            byte actor = frame.Core.Read(unchecked(header + 0x48 + (uint)index), 1)[0];
            frame = WriteAddressedByte(frame, unchecked(pointer + 0x1B), actor);
            incomingEcx = (temporary & 0xFFFFFF00) | actor;
            frame = NativeBattleWaterExecutionFields.WriteCoreDword(frame, 0x2797450, unchecked(NativeBattleWaterExecutionFields.CoreDword(frame, 0x2797450) - 4));
        }
        return NativeBattleWaterExecutionFields.WriteCoreDword(frame, 0x2797450, unchecked(NativeBattleWaterExecutionFields.CoreDword(frame, 0x2797450) + 4));
    }

    public static NativeBattleWaterExecutionFrame Execute9C(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank, uint incomingEcx)
    {
        frame = Spawn(frame, program, bank, incomingEcx);
        uint pointer = frame.ReadTemporaryDword(0xFC);
        ushort predecessor = ReadAddressedWord(frame, unchecked(pointer - 0xEE));
        frame = WriteAddressedWord(frame, unchecked(pointer + 0x14), predecessor);
        ushort ancestor = ReadAddressedWord(frame, unchecked(pointer - 0xEC));
        return WriteAddressedWord(frame, unchecked(pointer + 0x16), ancestor);
    }

    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank, uint incomingEcx)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        uint pc = NativeBattleWaterExecutionFields.CoreDword(frame, 0x2797450);
        switch (instruction & 0x1FF)
        {
            case 0x32:
                return Spawn(frame, program, bank, incomingEcx);
            case 0x26:
            case 0x8B:
                int stream = unchecked((short)Operand(2));
                if (stream == 0)
                {
                    stream = 1;
                    while (BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleWaterExecutionFields.ReadSlot(frame, 4 * stream, 4)) != 0)
                    {
                        stream++;
                        if (stream > 3)
                        {
                            return NativeBattleWaterExecutionFields.WriteCoreDword(frame, 0x2797450, unchecked(pc + 6));
                        }
                    }
                }
                uint streamTarget = unchecked(pc + (uint)(short)Operand(4));
                if ((instruction & 0x1FF) == 0x8B)
                {
                    streamTarget &= 0x7FFFFFFF;
                }
                frame = NativeBattleWaterExecutionFields.WriteSlotDword(frame, checked(4 * stream), streamTarget);
                frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)stream));
                frame = NativeBattleWaterExecutionFields.WriteSlotWord(frame, checked(0xC + 2 * stream), 0);
                int streamByte = NativeBattleWaterExecutionFields.SlotOffset(frame, checked(0x44 + stream), 1);
                frame = frame with { Arena = frame.Arena.Write(streamByte, new byte[] { 0 }) };
                return NativeBattleWaterExecutionFields.WriteCoreDword(frame, 0x2797450, unchecked(pc + 6));
            default:
                throw new NotSupportedException("Native child commands admit only physical child creation and two secondary stream operations.");
        }

        ushort Operand(int delta)
        {
            uint relative = unchecked(pc + (uint)delta - program.Address);
            if (program.ArenaOffset < 0 || program.Length < 2 || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - 2))
            {
                throw new InvalidDataException("Native child operand leaves its actual current arena program owner.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(checked(program.ArenaOffset + (int)relative), 2));
        }
    }

    public static NativeBattleWaterExecutionFrame Spawn(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank, uint incomingEcx)
    {
        uint pc = NativeBattleWaterExecutionFields.CoreDword(frame, 0x2797450);
        uint header = NativeBattleWaterExecutionFields.CoreDword(frame, 0x279744C);
        ushort capacity = NativeBattleWaterExecutionFields.CoreWord(frame, checked(header + 0x38));
        if (capacity is 0 or > 128)
        {
            throw new NotSupportedException("Native child creation requires its capacity within the physical active-order owner.");
        }
        if (frame.Core.Read(0x2797454 + (uint)capacity - 1, 1)[0] != 0xFF)
        {
            frame = frame.WriteTemporaryDword(0xFC, incomingEcx);
            return NativeBattleWaterExecutionFields.WriteCoreDword(frame, 0x2797450, unchecked(pc + 4));
        }
        int orderIndex = 0;
        while (frame.Core.Read(0x2797454 + (uint)orderIndex, 1)[0] != 0xFF)
        {
            orderIndex++;
            if (orderIndex >= capacity)
            {
                throw new NotSupportedException("Native child creation exhausted its active-order owner.");
            }
        }
        uint manager = NativeBattleWaterExecutionFields.CoreDword(frame, 0x27973EC);
        uint cached = NativeBattleWaterExecutionFields.CoreDword(frame, checked(manager + 0x90));
        NativeBattleWaterSlotBank currentBank = bank with { Capacity = capacity };
        if (cached != NativeBattleEffectNodeMemory.ArenaAddress + (uint)currentBank.SlotBaseOffset)
        {
            throw new InvalidDataException("Native child creation requires the actual cached slot-bank base.");
        }
        int child = 0;
        while (BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(currentBank.SlotOffset(frame.Arena, child), 4)) != 0)
        {
            child++;
            if (child >= capacity)
            {
                throw new NotSupportedException("Native child creation found no free physical slot within its current resource.");
            }
        }
        frame = frame with { Core = frame.Core.Write(0x2797454 + (uint)orderIndex, new byte[] { (byte)child }) };
        uint target = unchecked(pc + (uint)(short)Operand(2));
        int childOffset = currentBank.SlotOffset(frame.Arena, child);
        WriteChildDword(0, target);
        ushort active = NativeBattleWaterExecutionFields.CoreWord(frame, checked(header + 0x18));
        frame = NativeBattleWaterExecutionFields.WriteCoreWord(frame, checked(header + 0x18), unchecked((ushort)(active + 1)));
        uint command = NativeBattleWaterExecutionFields.CoreDword(frame, 0x27973C0);
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, currentBank, child, NativeBattleWaterExecutionFields.CoreWord(frame, checked(command + 0xC)), NativeBattleWaterExecutionFields.CoreWord(frame, checked(command + 0xE)), NativeBattleWaterExecutionFields.CoreWord(frame, checked(header + 0x1C))) };
        CopyParent(0x5C, 0x5C, 4);
        CopyParent(0x60, 0x60, 4);
        CopyParent(0x64, 0x64, 4);
        CopyParent(0x50, 0x50, 4);
        CopyParent(0x54, 0x54, 4);
        CopyParent(0x58, 0x58, 4);
        CopyParent(0x18, 0x18, 1);
        CopyParent(0x48, 0x48, 2);
        CopyParent(0x1B, 0x1B, 1);
        CopyParent(0xB0, 0xB0, 4);
        CopyParent(0xB4, 0xB4, 4);
        CopyParent(0x12, 0x14, 2);
        CopyParent(0x14, 0x16, 2);
        ushort id = NativeBattleWaterExecutionFields.CoreWord(frame, header);
        WriteChildWord(0x12, id);
        frame = NativeBattleWaterExecutionFields.WriteCoreWord(frame, header, unchecked((ushort)(id + 1)));
        uint parent = NativeBattleWaterExecutionFields.CoreDword(frame, 0x27973E8);
        uint childAddress = NativeBattleEffectNodeMemory.ArenaAddress + (uint)childOffset;
        frame = NativeBattleWaterExecutionFields.WriteCoreDword(frame, 0x27973E8, childAddress);
        byte updateType = frame.Arena.Read(childOffset + 0x18, 1)[0];
        frame = frame with { Arena = NativeBattleWaterSlots.UpdateSelected(frame.Arena, currentBank, child, updateType == 3 ? ActiveOrder(frame) : default) };
        frame = frame with { Arena = NativeBattleWaterSlots.CachePositionAA17D0(frame.Arena, currentBank, child) };
        frame = NativeBattleWaterExecutionFields.WriteCoreDword(frame, 0x27973E8, parent);
        frame = frame.WriteTemporaryDword(0xFC, childAddress);
        return NativeBattleWaterExecutionFields.WriteCoreDword(frame, 0x2797450, unchecked(NativeBattleWaterExecutionFields.CoreDword(frame, 0x2797450) + 4));

        void CopyParent(int source, int destination, int length)
        {
            byte[] bytes = NativeBattleWaterExecutionFields.ReadSlot(frame, source, length);
            frame = frame with { Arena = frame.Arena.Write(childOffset + destination, bytes) };
        }

        void WriteChildWord(int offset, ushort value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            frame = frame with { Arena = frame.Arena.Write(childOffset + offset, bytes) };
        }

        void WriteChildDword(int offset, uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            frame = frame with { Arena = frame.Arena.Write(childOffset + offset, bytes) };
        }

        ushort Operand(int delta)
        {
            uint relative = unchecked(pc + (uint)delta - program.Address);
            if (program.ArenaOffset < 0 || program.Length < 2 || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - 2))
            {
                throw new InvalidDataException("Native child operand leaves its actual current arena program owner.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(checked(program.ArenaOffset + (int)relative), 2));
        }
    }

    private static NativeBattleWaterExecutionFrame WriteAddressedByte(NativeBattleWaterExecutionFrame frame, uint address, byte value)
    {
        if (address >= frame.TemporaryFrame.Address && (long)address - frame.TemporaryFrame.Address <= 0x17F)
        {
            return frame.WriteTemporaryByte(checked((int)(address - frame.TemporaryFrame.Address)), value);
        }
        if (address >= NativeBattleScriptedEffectMemory.Address && (long)address - NativeBattleScriptedEffectMemory.Address < NativeBattleScriptedEffectMemory.Length)
        {
            return frame with { Core = frame.Core.Write(address, [value]) };
        }
        return frame with { Arena = frame.Arena.Write(NativeBattleWaterExecutionFields.ArenaOffset(address, 1), [value]) };
    }

    private static ushort ReadAddressedWord(NativeBattleWaterExecutionFrame frame, uint address)
    {
        if (address >= frame.TemporaryFrame.Address && (long)address - frame.TemporaryFrame.Address <= 0x17E)
        {
            return frame.ReadTemporaryWord(checked((int)(address - frame.TemporaryFrame.Address)));
        }
        if (address >= NativeBattleScriptedEffectMemory.Address && (long)address - NativeBattleScriptedEffectMemory.Address <= NativeBattleScriptedEffectMemory.Length - 2)
        {
            return NativeBattleWaterExecutionFields.CoreWord(frame, address);
        }
        return BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, 2), 2));
    }

    private static NativeBattleWaterExecutionFrame WriteAddressedWord(NativeBattleWaterExecutionFrame frame, uint address, ushort value)
    {
        if (address >= frame.TemporaryFrame.Address && (long)address - frame.TemporaryFrame.Address <= 0x17E)
        {
            return frame.WriteTemporaryWord(checked((int)(address - frame.TemporaryFrame.Address)), value);
        }
        if (address >= NativeBattleScriptedEffectMemory.Address && (long)address - NativeBattleScriptedEffectMemory.Address <= NativeBattleScriptedEffectMemory.Length - 2)
        {
            return NativeBattleWaterExecutionFields.WriteCoreWord(frame, address, value);
        }
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(NativeBattleWaterExecutionFields.ArenaOffset(address, 2), bytes) };
    }

    private static ImmutableArray<byte> ActiveOrder(NativeBattleWaterExecutionFrame frame)
    {
        //The unused suffix is a transient argument placeholder, not a claim about physical bytes beyond FF.
        byte[] order = Enumerable.Repeat((byte)0xFF, 128).ToArray();
        for (int index = 0; index < order.Length; index++)
        {
            order[index] = frame.Core.Read(0x2797454 + (uint)index, 1)[0];
            if (order[index] == 0xFF)
            {
                return order.ToImmutableArray();
            }
        }
        throw new NotSupportedException("Native child update requires its actual terminated active-order owner.");
    }
}
