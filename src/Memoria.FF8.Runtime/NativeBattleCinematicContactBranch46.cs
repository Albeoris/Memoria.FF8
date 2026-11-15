using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Branches on the first matching current contact's result flags or the participant's battle status.</summary>
public static class NativeBattleCinematicContactBranch46
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterContextServices services, Func<NativeBattleWaterExecutionFrame, BattleModelOwner, uint> readBattleStatus)
    {
        BattleModelOwner participant = NativeBattleCinematicActorAccess.ReadSlotActor(frame);
        int mode = ReadMode();
        if (mode == 4)
        {
            return Advance((readBattleStatus(frame, participant) & 0x2000) != 0);
        }
        BattleActionHeaderReference header = services.RequireCombatHeader(frame);
        NativeBattleActionPacketsState packets = services.ReadPackets();
        BattleContactCursor first = packets.ReadHeader(header).FirstContact;
        int count = packets.ReadHeader(header).ReadContactCount();
        int index = 0;
        for (; index < count; index++)
        {
            if (packets.ReadContact(packets.ResolveContact(header, index)).Primary.Recipient == participant)
            {
                break;
            }
        }
        NativeBattleContactHalf primary = packets.ReadContact(first.Advance(index)).Primary;
        byte flags = primary.HitFlags;
        bool branch = mode switch
        {
            1 => (flags & 4) != 0,
            2 => (primary.Flags2 & 8) != 0,
            3 => (flags & 2) != 0,
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
    }
}
