using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterPresentationServices(Func<NativeBattleWaterExecutionFrame, uint, ushort> ReadActorFlags, Func<NativeBattleWaterExecutionFrame, uint, ushort, NativeBattleWaterExecutionFrame> WriteActorFlags, Func<NativeBattleWaterExecutionFrame, uint, short, byte, NativeBattleWaterExecutionFrame> PlaySound501330)
{
    public Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame>? UpdateSlot { get; init; }
}

/// <summary>Selected draw registration, cached actor position, actor flags and sound commands.</summary>
public static class NativeBattleWaterPresentationCommands
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank, int currentIndex, NativeBattleWaterPresentationServices services)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        uint pc = CoreDword(frame, 0x2797450);
        switch (instruction & 0x1FF)
        {
            case 0x43:
            case 0x44:
                short kind = unchecked((short)Word(unchecked(pc + 2)));
                frame = NativeBattleWaterParticleCommands.RegisterDrawB65320(frame, unchecked((byte)kind));
                pc = CoreDword(frame, 0x2797450);
                short count = unchecked((short)Word(unchecked(pc + 4)));
                frame = WriteSlotDword(frame, 0xB8, unchecked(pc + 6));
                frame = WriteSlotDword(frame, 0xBC, 0);
                if ((instruction & 0x1FF) == 0x44)
                {
                    frame = WriteSlotDword(frame, 0xC0, 0);
                    frame = WriteSlotDword(frame, 0xC4, 0);
                }
                return WriteCoreDword(frame, 0x2797450, unchecked(pc + 6u + (uint)(2 * count)));
            case 0x4D:
                frame = SlotByte(frame, 0x18, 0);
                frame = WriteSlotWord(frame, 0x48, 0);
                frame = NativeBattleWaterParticleCommands.RegisterDrawB65320(frame, 4);
                return Advance(2);
            case 0xD0:
                frame = NativeBattleWaterStopCommand.RemoveDrawB65270(frame, frame.ReadTemporaryByte(0x142));
                return Advance(2);
            case 0x5A:
                frame = SlotByte(frame, 0x18, ReadProgram(unchecked(pc + 2), 1)[0]);
                pc = CoreDword(frame, 0x2797450);
                frame = WriteSlotWord(frame, 0xB0, Word(unchecked(pc + 4)));
                frame = Update(frame);
                return Advance(6);
            case 0x5E:
                int actor = Actor(frame);
                uint header = CoreDword(frame, 0x279744C);
                for (int lane = 0; lane < 3; lane++)
                {
                    short position = unchecked((short)CoreWord(frame, unchecked(header + 0x84u + (uint)(8 * actor + 2 * lane))));
                    frame = WriteSlotDword(frame, 0x5C + 4 * lane, unchecked((uint)(position << 16)));
                }
                frame = Update(frame);
                frame = frame with { Arena = NativeBattleWaterSlots.CachePositionAA17D0(frame.Arena, bank, currentIndex) };
                return Advance(2);
            case 0x45:
                int flagActor = Actor(frame);
                uint flagHeader = CoreDword(frame, 0x279744C);
                uint actorPointer = CoreDword(frame, unchecked(flagHeader + 0x60u + (uint)(4 * flagActor)));
                int prior = unchecked((short)CoreWord(frame, unchecked(flagHeader + 0x4Eu + (uint)(2 * flagActor))));
                int mask = unchecked((short)Word(unchecked(pc + 2)));
                int mode = instruction >> 12;
                if (mode != 0 && mode != 1 && mode != 8)
                {
                    throw new NotSupportedException("Native selected actor flags require modes zero, one or eight.");
                }
                if (mode != 1 || (mask & prior) == 0)
                {
                    ushort flags = services.ReadActorFlags(frame, actorPointer);
                    ushort output = mode == 0 ? unchecked((ushort)(flags | mask)) : unchecked((ushort)(flags & ~mask));
                    frame = services.WriteActorFlags(frame, actorPointer, output);
                }
                return Advance(4);
            case 0x2B:
                uint manager = CoreDword(frame, 0x27973EC);
                uint directory = CoreDword(frame, unchecked(manager + 0xC4));
                uint descriptor = unchecked(directory + BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(directory + (uint)(4 * (instruction >> 9))), 4), 4)));
                short argument = unchecked((short)Word(unchecked(pc + 2)));
                frame = services.PlaySound501330(frame, descriptor, argument, 0x80);
                return Advance(4);
            default:
                throw new NotSupportedException("Native presentation commands admit only their selected finite operations.");
        }

        NativeBattleWaterExecutionFrame Advance(uint count) => WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + count));

        byte[] ReadProgram(uint address, int length)
        {
            uint relative = unchecked(address - program.Address);
            if (program.ArenaOffset < 0 || program.Length < length || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - length))
            {
                throw new InvalidDataException("Native presentation operand leaves its current program owner.");
            }
            return frame.Arena.Read(checked(program.ArenaOffset + (int)relative), length);
        }

        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(ReadProgram(address, 2));

        NativeBattleWaterExecutionFrame Update(NativeBattleWaterExecutionFrame current)
        {
            int slot = bank.SlotOffset(current.Arena, currentIndex);
            if (CoreDword(current, 0x27973E8) != NativeBattleEffectNodeMemory.ArenaAddress + (uint)slot)
            {
                throw new InvalidDataException("Native selected update requires its actual current cached-bank slot.");
            }
            byte kind = ReadSlot(current, 0x18, 1)[0];
            if (services.UpdateSlot is { } selectedUpdate)
            {
                return selectedUpdate(current, kind);
            }
            ImmutableArray<byte> active = [];
            if (kind is 3 or 5 or 7)
            {
                byte[] order = Enumerable.Repeat((byte)0xFF, 128).ToArray();
                for (int i = 0; i < order.Length; i++)
                {
                    order[i] = current.Core.Read(0x2797454 + (uint)i, 1)[0];
                    if (order[i] == 0xFF)
                    {
                        break;
                    }
                }
                active = order.ToImmutableArray();
            }
            return current with { Arena = NativeBattleWaterSlots.UpdateSelected(current.Arena, bank, currentIndex, active) };
        }
    }

    private static int Actor(NativeBattleWaterExecutionFrame frame)
    {
        int actor = ReadSlot(frame, 0x1B, 1)[0];
        return actor < 7 ? actor : throw new NotSupportedException("Native cached actor access leaves its seven physical rows.");
    }

    private static NativeBattleWaterExecutionFrame SlotByte(NativeBattleWaterExecutionFrame frame, int offset, byte value) => frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 1), new byte[] { value }) };
}
