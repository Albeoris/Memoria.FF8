using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleScriptedVisibility
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, BattleSide?, Func<NativeBattleGraphicsModel, Func<ushort>, NativeBattleGraphicsModel>, NativeBattleWaterExecutionFrame> updateModels)
    {
        if ((frame.ReadTemporaryWord(0x14A) & 0x1FF) != 0x13D)
        {
            throw new NotSupportedException("Native visibility command requires its selected instruction.");
        }
        uint manager = CoreDword(frame, 0x27973EC);
        uint root = CoreDword(frame, manager + 0x90);
        BattleModelOwner source = frame.Arena.ReadExecutionReference<BattleExecutionReference.Model>(ArenaOffset(unchecked(root + 0x1B), 1), 1).Owner;
        bool party = source.Side == BattleSide.Allies;
        short operand = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        bool hide = operand is 1 or 3 or 5;
        bool all = operand is 4 or 5;
        bool selectedParty = operand is 2 or 3 ? !party : party;
        BattleSide? side = all ? null : selectedParty ? BattleSide.Allies : BattleSide.Enemies;
        frame = updateModels(frame, side, (model, readOriginalFlags) =>
        {
            if (!hide && (readOriginalFlags() & 4) != 0)
            {
                return model;
            }
            return model with { Flags0 = hide ? (ushort)(model.Flags0 | 4) : (ushort)(model.Flags0 & 0xFFFB) };
        });
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }
}
