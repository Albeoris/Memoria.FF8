using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicActorCommandServices(Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> DisableMenu, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> BeginOutcome, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> ResetStanding);

public static class NativeBattleCinematicActorCommands77
{
    public static NativeBattleWaterExecutionFrame BranchBE(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<uint, byte> readModelFlags)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) != 0xBE)
        {
            throw new InvalidDataException("Native cinematic actor branch requires instructionBE.");
        }
        bool loaded = (instruction & 0xF000) == 0 && (readModelFlags(SelectedActor(frame)) & 2) != 0;
        uint delta = loaded ? 4u : unchecked((uint)(int)BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2)));
        return NativeBattleScriptedCommandReads.Advance(frame, delta);
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

    private static uint SelectedActor(NativeBattleWaterExecutionFrame frame)
    {
        byte slot = ReadSlot(frame, 0x1B, 1)[0];
        uint directory = CoreDword(frame, 0x279744C);
        return CoreDword(frame, unchecked(directory + 0x60u + 4u * slot));
    }
}
