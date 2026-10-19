using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicGridActor3C(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleGraphicsState graphics)
    {
        NativeBattleCinematicGridActorServices services = new(address => graphics.Models[Index(address)], (address, actor) => graphics = graphics with { Models = graphics.Models.SetItem(Index(address), actor) }, (current, selector) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int index = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, index));
        }, TransformNativeCinematicCurve);
        return (NativeBattleCinematicGridActor3C.Draw(frame, services), graphics);

        int Index(uint address)
        {
            if (address < 0x1D972C0 || (address - 0x1D972C0) % 156 != 0 || (address - 0x1D972C0) / 156 >= graphics.Models.Length)
            {
                throw new NotSupportedException("Native grid actor requires its actual physical model pointer.");
            }
            return checked((int)((address - 0x1D972C0) / 156));
        }
    }
}
