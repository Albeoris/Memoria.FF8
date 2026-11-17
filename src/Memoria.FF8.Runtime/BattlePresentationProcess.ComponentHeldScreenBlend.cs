using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    /// <summary>Observe the original resident constant screen blend; neither release it nor stop its native rectangle publication.</summary>
    internal BattleComponentHeldScreenBlendObservation? ObserveComponentHeldScreenBlend(ushort expectedEffect, BattleDiagnosticInvocationOwners owners, ImmutableArray<string> blockers)
    {
        if (NativeGraphics is not { OriginalEffect: { ReturnedDescriptor: 0 } selected } graphics || selected.RequestedEffect != expectedEffect || selected.Binding.Effect != expectedEffect || NativeEffectMemory is not { Active: 0 } memory)
        {
            return null;
        }
        nativeActionPackets.Validate();
        NativeBattleAuxiliaryState auxiliary = graphics.Auxiliary;
        auxiliary.Validate();
        int[] active = Enumerable.Range(0, auxiliary.Slots.Length).Where(slot => (auxiliary.Slots[slot].Flags & 1) != 0).ToArray();
        if (active.Length != 1 || auxiliary.Head != active[0] || auxiliary.Tail != active[0])
        {
            return null;
        }
        int index = active[0];
        NativeBattleAuxiliaryTask task = auxiliary.Slots[index];
        if (task.Kind != NativeBattleAuxiliaryKind.ScreenBlend501E80)
        {
            return null;
        }
        // The blend reads only the timing and color lanes. A reused slot can still retain another callback's operand program.
        BattleComponentHeldScreenBlendTask held = new(index, task.Flags, task.State, task.Next, task.Kind, task.UnknownByteMask, false, task.Payload);
        if (!held.IsHeld)
        {
            return null;
        }
        BattleComponentHeldScreenBlendObservation observation = new(expectedEffect, graphics.Callback, 0, checked((uint)memory.ReadReturnedTask5044B0()), owners, active.Length, auxiliary.Head, auxiliary.Tail, held, held.Fingerprint, blockers);
        return observation.IsEligible ? observation : null;
    }
}
