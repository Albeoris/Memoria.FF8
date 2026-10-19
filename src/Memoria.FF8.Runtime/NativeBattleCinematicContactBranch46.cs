using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Branches on the first matching current contact's result flags or the participant's battle status.</summary>
public static class NativeBattleCinematicContactBranch46
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, uint, int, byte[]> readAction, Func<NativeBattleWaterExecutionFrame, byte, uint> readBattleStatus)
    {
        uint manager = CoreDword(frame, 0x27973EC);
        uint action = CoreDword(frame, unchecked(manager + 0xCC));
        byte participant = ReadSlot(frame, 0x1B, 1)[0];
        uint contact = BinaryPrimitives.ReadUInt32LittleEndian(Read(unchecked(action + 8), 4));
        int count = Read(unchecked(action + 0x10), 1)[0];
        for (int index = 0; index < count; index++)
        {
            if (Read(contact, 1)[0] == participant)
            {
                break;
            }
            contact = unchecked(contact + 0x18);
        }
        uint modeAddress = unchecked(CoreDword(frame, 0x27973B8) + 0x4A);
        ushort raw = modeAddress >= frame.TemporaryFrame.Address && (long)modeAddress - frame.TemporaryFrame.Address <= 0x17E ? frame.ReadTemporaryWord(checked((int)(modeAddress - frame.TemporaryFrame.Address))) : BinaryPrimitives.ReadUInt16LittleEndian(frame.Core.Read(modeAddress, 2));
        byte flags = Read(unchecked(contact + 3), 1)[0];
        bool branch = (raw >> 9) switch
        {
            1 => (flags & 4) != 0,
            2 => (Read(unchecked(contact + 2), 1)[0] & 8) != 0,
            3 => (flags & 2) != 0,
            4 => (readBattleStatus(frame, participant) & 0x2000) != 0,
            _ => (flags & 4) == 0
        };
        uint advance = branch ? unchecked((uint)(int)BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2))) : 4u;
        return NativeBattleScriptedCommandReads.Advance(frame, advance);

        byte[] Read(uint address, int length)
        {
            if (address >= NativeBattleEffectNodeMemory.ArenaAddress && (long)address - NativeBattleEffectNodeMemory.ArenaAddress <= NativeBattleEffectArena.Length - length)
            {
                return frame.Arena.Read(ArenaOffset(address, length), length);
            }
            byte[] bytes = readAction(frame, address, length);
            return bytes is not null && bytes.Length == length ? bytes : throw new InvalidDataException("Native cinematic contact branch requires its exact current addressed packet bytes.");
        }
    }
}
