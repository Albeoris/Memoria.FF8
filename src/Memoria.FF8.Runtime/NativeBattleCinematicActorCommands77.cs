using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicActorCommandServices(Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> DisableMenu, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> BeginOutcome, Func<NativeBattleWaterExecutionFrame, BattleModelOwner, NativeBattleWaterExecutionFrame> ResetStanding);

public static class NativeBattleCinematicActorCommands77
{
    public static NativeBattleWaterExecutionFrame BranchBE(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<BattleModelOwner, byte> readModelFlags, Func<NativeBattleModelSlot, byte>? readOriginalFlags = null)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) != 0xBE)
        {
            throw new InvalidDataException("Native cinematic actor branch requires instructionBE.");
        }
        bool loaded = (instruction & 0xF000) == 0 && (ReadFlags() & 2) != 0;
        uint delta = loaded ? 4u : unchecked((uint)(int)BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2)));
        return NativeBattleScriptedCommandReads.Advance(frame, delta);

        byte ReadFlags()
        {
            return NativeBattleCinematicActorAccess.ResolveSlotOriginalCell(frame) is { } slot
                ? (readOriginalFlags ?? throw new NotSupportedException("Native cinematic actor branch requires its current original model cell."))(slot)
                : readModelFlags(SelectedActor(frame));
        }
    }

    public static NativeBattleWaterExecutionFrame ExecuteC1(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicActorCommandServices services)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) != 0xC1)
        {
            throw new InvalidDataException("Native cinematic actor service requires instructionC1.");
        }
        frame = (instruction >> 12) switch
        {
            0 => services.DisableMenu(frame),
            1 => services.BeginOutcome(frame),
            2 => services.ResetStanding(frame, SelectedActor(frame)),
            _ => frame
        };
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }

    private static BattleModelOwner SelectedActor(NativeBattleWaterExecutionFrame frame)
    {
        return NativeBattleCinematicActorAccess.ReadSlotActor(frame);
    }
}
