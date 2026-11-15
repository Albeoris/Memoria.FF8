using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicGridActor3C(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleGraphicsState graphics)
    {
        NativeBattleCinematicGridActorServices services = new(address => graphics.ActorModels.Get(address), (address, actor) => graphics = graphics with { ActorModels = graphics.ActorModels.Set(address, actor) }, (current, selector) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int index = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, index));
        }, TransformNativeCinematicCurve);
        return (NativeBattleCinematicGridActor3C.Draw(frame, services), graphics);
    }
}
