using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicLeviathanModel20(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicResources resources, NativeBattleCinematicModelServices modelServices, NativeBattleGraphicsState graphics)
    {
        NativeBattleCinematicLeviathanModelServices services = new(modelServices, (current, shape) => NativeBattleWaterShapeSelection.ResolveCinematic(current, resources, shape).Selected, (current, selector) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int selected = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, selected));
        }, current =>
        {
            NativeBattleVector point = new(unchecked((short)SlotWord(current, 0x94)), unchecked((short)SlotWord(current, 0x96)), unchecked((short)SlotWord(current, 0x98)));
            return NativeBattleCinematicScaledBasis.Prepare(current, resourceLength, point).Frame;
        });
        return DrawNativeCinematicModel(frame, resourceLength, modelServices, graphics, current => NativeBattleCinematicModel.PrepareLeviathan20(current, services));
    }
}
