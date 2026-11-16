using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleResonance49Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleResonance49Controller
{
    private const int ScreenReactionPoolIndex = 4;

    private static int ScreenReactionCapacity(NativeBattleResonance49Frame frame, NativeBattleResonance49Inputs inputs)
    {
        int original = Pools[ScreenReactionPoolIndex].Capacity;
        if (inputs.StorageContract != BattleContactTaskStorageContract.ExpandableArena)
        {
            return original;
        }
        NativeBattleActionHeaderSlot header = inputs.Packets.ReadHeader(Parameters(frame).Header);
        int required = checked(1 + header.ReadContactCount());
        if (required <= original)
        {
            return original;
        }
        //821B30 emits one screen and one reaction per contact. Multiple header groups can overlap descendants.
        if (header.ReadRepeatModeMinusOne() != 0)
        {
            throw new NotSupportedException("Expanded Resonance reaction storage requires its single-header lifetime contract.");
        }
        return checked((ushort)required);
    }

    private static (NativeBattleResonance49Frame Frame, NativeBattleEffectHierarchy Hierarchy) InitializeHierarchy(NativeBattleResonance49Frame frame, NativeBattleResonance49Inputs inputs)
    {
        int capacity = ScreenReactionCapacity(frame, inputs);
        NativeBattleCureFamilyPool reaction = Pools[ScreenReactionPoolIndex];
        uint bank = reaction.Bank;
        if (capacity > reaction.Capacity)
        {
            (NativeBattleEffectMemory memory, bank) = NativeBattlePresentationTaskStorage.Reserve(frame.Memory, frame.Effect, inputs.Packets.Roster, reaction.Stride, capacity);
            frame = frame with { Memory = memory };
        }
        ImmutableArray<NativeBattleEffectTaskPool> pools = Pools.Select((pool, index) => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, index == ScreenReactionPoolIndex ? bank : pool.Bank, pool.Stride, index == ScreenReactionPoolIndex ? checked((ushort)capacity) : pool.Capacity)).ToImmutableArray();
        return (frame, new(pools));
    }

    private static NativeBattleEffectArena CaptureInactiveBank(NativeBattleEffectTaskPool pool, int index)
    {
        NativeBattleEffectArena storage = NativeBattleEffectTaskMemory.CaptureBank(pool);
        NativeBattleCureFamilyPool original = Pools[index];
        if (pool.Slots.Length == original.Capacity)
        {
            return storage;
        }
        //All descendants have retired. Preserve the original dormant byte/knownness prefix; entry resets it before reuse.
        return NativeBattleEffectNodeContextMemory.Copy(storage, 0, NativeBattleEffectArena.Clear(), 0, checked(original.Stride * original.Capacity));
    }
}
