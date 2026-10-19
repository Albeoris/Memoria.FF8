using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Selected matrix copy with literal signed byte displacements and native paired DWORD load ordering.</summary>
public static class NativeBattleCinematicMatrixCopy84
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, short, uint> resolveSlot)
    {
        uint caller = CoreDword(frame, 0x27973E8);
        uint selected = resolveSlot(frame, Operand(2));
        frame = WriteCoreDword(frame, 0x27973E8, selected);
        short selectedOffset = Operand(4);
        uint source;
        if (selectedOffset == 0)
        {
            (frame, source) = NativeBattleWaterMatrixCommands.AllocateB65480(frame);
        }
        else
        {
            source = unchecked(selected + (uint)selectedOffset);
        }
        frame = WriteCoreDword(frame, 0x27973E8, caller);
        short currentOffset = Operand(6);
        uint destination;
        if (currentOffset == 0)
        {
            (frame, destination) = NativeBattleWaterMatrixCommands.AllocateB65480(frame);
        }
        else
        {
            destination = unchecked(caller + (uint)currentOffset);
        }
        for (uint offset = 0; offset < 32; offset += 8)
        {
            uint from = unchecked(source + offset);
            uint to = unchecked(destination + offset);
            var snapshot = IsCore(from) ? frame.Core.ReadSnapshot(from, 8) : frame.Arena.ReadSnapshot(ArenaOffset(from, 8), 8);
            bool core = IsCore(to);
            if (core)
            {
                frame = frame with { Core = frame.Core.Write(to, snapshot.Item1) };
            }
            else
            {
                frame = frame with { Arena = frame.Arena.Write(ArenaOffset(to, 8), snapshot.Item1) };
            }
            for (int lane = 0; lane < 8; lane++)
            {
                if (!snapshot.Item2[lane])
                {
                    uint address = unchecked(to + (uint)lane);
                    frame = core ? frame with { Core = frame.Core.MarkUnknown(address, 1) } : frame with { Arena = frame.Arena.MarkUnknown(ArenaOffset(address, 1), 1) };
                }
            }
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 8);

        short Operand(uint delta) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, delta, 2));
        static bool IsCore(uint address) => address >= NativeBattleScriptedEffectMemory.Address && (long)address - NativeBattleScriptedEffectMemory.Address <= NativeBattleScriptedEffectMemory.Length - 8;
    }
}
