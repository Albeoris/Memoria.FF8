using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The two signed-PC scheduling passes around an explicit finite command service.</summary>
public static class NativeBattleWaterScriptScheduler
{
    public static NativeBattleWaterExecutionFrame RunScriptPass(bool prePass, NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, uint, ushort> readInstruction, Func<NativeBattleWaterExecutionFrame, ushort, NativeBattleWaterExecutionFrame> executeOne, int instructionBudget = 4096)
    {
        ArgumentNullException.ThrowIfNull(readInstruction);
        ArgumentNullException.ThrowIfNull(executeOne);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(instructionBudget);
        frame = frame.WriteTemporaryWord(0x13E, 0);
        if (prePass)
        {
            frame.TemporaryFrame.Copy(0x138, 0x14C, 4);
        }
        frame = frame.WriteTemporaryByte(0x144, 0);
        int executed = 0;
        while (true)
        {
            int cursor = frame.ReadTemporaryByte(0x144);
            if (cursor >= 128)
            {
                throw new NotSupportedException("Native script traversal left its bounded active-order owner before its terminator.");
            }
            byte entry = frame.Core.Read(0x2797454 + (uint)cursor, 1)[0];
            if (entry == 0xFF)
            {
                return frame;
            }
            if (frame.ReadTemporaryByte(0x145) == 0 || (entry & 0x80) != 0)
            {
                frame = frame.WriteTemporaryByte(0x142, (byte)(entry & 0x7F));
                uint manager = NativeBattleWaterExecutionFields.CoreDword(frame, 0x27973EC);
                uint bank = NativeBattleWaterExecutionFields.CoreDword(frame, checked(manager + 0x90));
                frame = NativeBattleWaterExecutionFields.WriteCoreDword(frame, 0x27973E8, unchecked(bank + (uint)(0x100 * (entry & 0x7F))));
                frame = frame.WriteTemporaryByte(0x143, 0);
                while (frame.ReadTemporaryByte(0x143) < 3)
                {
                    int stream = frame.ReadTemporaryByte(0x143);
                    uint pc = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleWaterExecutionFields.ReadSlot(frame, 4 * stream, 4));
                    frame = NativeBattleWaterExecutionFields.WriteCoreDword(frame, 0x2797450, pc);
                    bool eligible = prePass ? unchecked((int)pc) < 0 : unchecked((int)pc) > 0;
                    if (eligible)
                    {
                        short wait = unchecked((short)NativeBattleWaterExecutionFields.SlotWord(frame, 0xC + 2 * stream));
                        if (wait > 0)
                        {
                            ushort tick = NativeBattleWaterExecutionFields.SlotWord(frame, 0xC8);
                            frame = NativeBattleWaterExecutionFields.WriteSlotWord(frame, 0xC + 2 * stream, unchecked((ushort)(wait - tick)));
                        }
                        else
                        {
                            do
                            {
                                if (executed >= instructionBudget)
                                {
                                    throw new NotSupportedException("Native script diagnostic instruction budget was exhausted without a native yield.");
                                }
                                ushort instruction = readInstruction(frame, NativeBattleWaterExecutionFields.CoreDword(frame, 0x2797450));
                                frame = frame.WriteTemporaryWord(0x14A, instruction);
                                executed++;
                                frame = executeOne(frame, (ushort)(instruction & 0x1FF)) ?? throw new InvalidDataException("Native command service lost its current execution owners.");
                            }
                            while (frame.ReadTemporaryWord(0x13E) == 0);
                            stream = frame.ReadTemporaryByte(0x143);
                            ushort tick = NativeBattleWaterExecutionFields.SlotWord(frame, 0xC8);
                            ushort debt = NativeBattleWaterExecutionFields.SlotWord(frame, 0xC + 2 * stream);
                            ushort waitDelta = unchecked((ushort)(frame.ReadTemporaryWord(0x13E) - tick));
                            frame = NativeBattleWaterExecutionFields.WriteSlotWord(frame, 0xC + 2 * stream, unchecked((ushort)(debt + waitDelta)));
                            frame = frame.WriteTemporaryWord(0x13E, 0);
                            stream = frame.ReadTemporaryByte(0x143);
                            frame = NativeBattleWaterExecutionFields.WriteSlotDword(frame, stream * 4, NativeBattleWaterExecutionFields.CoreDword(frame, 0x2797450));
                        }
                    }
                    frame = frame.WriteTemporaryByte(0x143, unchecked((byte)(frame.ReadTemporaryByte(0x143) + 1)));
                }
            }
            frame = frame.WriteTemporaryByte(0x144, unchecked((byte)(frame.ReadTemporaryByte(0x144) + 1)));
        }
    }
}

internal static class NativeBattleWaterExecutionFields
{
    public static uint CoreDword(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Core.Read(address, 4));
    public static ushort CoreWord(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Core.Read(address, 2));
    public static ushort SlotWord(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(ReadSlot(frame, offset, 2));

    public static int ArenaOffset(uint address, int length)
    {
        long offset = (long)address - NativeBattleEffectNodeMemory.ArenaAddress;
        if (length < 0 || offset < 0 || offset > NativeBattleEffectArena.Length - length)
        {
            throw new NotSupportedException("Native scripted-effect pointer leaves its actual resource arena.");
        }
        return (int)offset;
    }

    public static byte[] ReadSlot(NativeBattleWaterExecutionFrame frame, int offset, int length) => frame.Arena.Read(SlotOffset(frame, offset, length), length);

    public static int SlotOffset(NativeBattleWaterExecutionFrame frame, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > 0x100 - length)
        {
            throw new NotSupportedException("Native current-slot access leaves its physical 256-byte owner.");
        }
        return checked(ArenaOffset(CoreDword(frame, 0x27973E8), 0x100) + offset);
    }

    public static NativeBattleWaterExecutionFrame WriteCoreDword(NativeBattleWaterExecutionFrame frame, uint address, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Core = frame.Core.Write(address, bytes) };
    }

    public static NativeBattleWaterExecutionFrame WriteCoreWord(NativeBattleWaterExecutionFrame frame, uint address, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return frame with { Core = frame.Core.Write(address, bytes) };
    }

    public static NativeBattleWaterExecutionFrame WriteSlotWord(NativeBattleWaterExecutionFrame frame, int offset, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 2), bytes) };
    }

    public static NativeBattleWaterExecutionFrame WriteSlotDword(NativeBattleWaterExecutionFrame frame, int offset, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 4), bytes) };
    }
}
