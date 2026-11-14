using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Branches on the first matching current contact's result flags or the participant's battle status.</summary>
public static class NativeBattleCinematicContactBranch46
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, uint, int, byte[]> readAction, Func<NativeBattleWaterExecutionFrame, byte, uint> readBattleStatus, NativeBattleActionPacketsState? packets = null)
    {
        uint manager = CoreDword(frame, 0x27973EC);
        uint action = CoreDword(frame, unchecked(manager + 0xCC));
        byte participant = ReadSlot(frame, 0x1B, 1)[0];
        bool hosted = packets?.PresentationRoster is not null;
        int? hostedMode = hosted ? ReadMode() : null;
        if (hostedMode == 4)
        {
            return Advance((readBattleStatus(frame, participant) & 0x2000) != 0);
        }
        uint contact = BinaryPrimitives.ReadUInt32LittleEndian(Read(unchecked(action + 8), 4));
        int count = NativeBattleCinematicContacts.ReadCount(frame, action, readAction, packets, signedNative: false);
        uint first = contact;
        int index = 0;
        for (; index < count; index++)
        {
            if (Read(contact, 1)[0] == participant)
            {
                break;
            }
            if (!hosted || index + 1 < count)
            {
                contact = NativeBattleCinematicContacts.Offset(first, index + 1, packets);
            }
        }
        if (hosted && index == count)
        {
            throw new InvalidDataException("Hosted cinematic contact branch has no contact for the current actor.");
        }
        int mode = hostedMode ?? ReadMode();
        byte flags = Read(unchecked(contact + 3), 1)[0];
        bool branch = mode switch
        {
            1 => (flags & 4) != 0,
            2 => (Read(unchecked(contact + 2), 1)[0] & 8) != 0,
            3 => (flags & 2) != 0,
            4 => (readBattleStatus(frame, participant) & 0x2000) != 0,
            _ => (flags & 4) == 0
        };
        return Advance(branch);

        int ReadMode()
        {
            uint address = unchecked(CoreDword(frame, 0x27973B8) + 0x4A);
            ushort raw = address >= frame.TemporaryFrame.Address && (long)address - frame.TemporaryFrame.Address <= 0x17E ? frame.ReadTemporaryWord(checked((int)(address - frame.TemporaryFrame.Address))) : BinaryPrimitives.ReadUInt16LittleEndian(frame.Core.Read(address, 2));
            return raw >> 9;
        }

        NativeBattleWaterExecutionFrame Advance(bool taken)
        {
            uint amount = taken ? unchecked((uint)(int)BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2))) : 4u;
            return NativeBattleScriptedCommandReads.Advance(frame, amount);
        }

        byte[] Read(uint address, int length) => NativeBattleCinematicContacts.ReadBytes(frame, address, length, readAction);
    }
}
