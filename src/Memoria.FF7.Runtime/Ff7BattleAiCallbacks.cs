namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattleAiMasks(ushort Allocated, ushort Targetable, ushort ScriptActive, ushort KnockedOut, ushort ExcludedOpponents, ushort TurnEligible)
{
    public ushort GaugeEligible => (ushort)((TurnEligible | 0x3F0) & (ScriptActive | 7));
}
public sealed record Ff7BattleAiCounterPlan(Ff7BattleAiActor Target, ushort ProcessedMask, byte[] Callbacks);

public static class Ff7BattleAiCallbacks
{
    public static Ff7BattleAiMasks ProjectMasks(IReadOnlyList<Ff7BattleAiActor> actors, ushort specialMode)
    {
        ArgumentNullException.ThrowIfNull(actors);
        ushort allocated = 0;
        ushort targetable = 0;
        ushort active = 0;
        ushort knockedOut = 0;
        ushort excluded = 0;
        uint knockoutStatuses = specialMode is 0 or 3 ? 0x80000001U : 1U;
        foreach (Ff7BattleAiActor actor in actors)
        {
            ushort bit = SlotBit(actor.Slot);
            if ((allocated & bit) != 0)
            {
                throw new InvalidDataException("FFVII battle actor mask has a repeated slot.");
            }
            allocated |= bit;
            if ((actor.Conditions & 0x08) != 0)
            {
                targetable |= bit;
            }
            if ((actor.Conditions & 0x10) != 0)
            {
                active |= bit;
            }
            if ((actor.Statuses & knockoutStatuses) != 0)
            {
                knockedOut |= bit;
            }
            if ((actor.Statuses & 0x4000) != 0)
            {
                excluded |= bit;
            }
        }
        ushort turnEligible = (ushort)(allocated & ((~knockedOut & 0x00F) | ((active | targetable) & 0x3F0)));
        return new(allocated, targetable, active, knockedOut, excluded, turnEligible);
    }

    /// <summary>Selects the callbacks for one physical recipient of a completed native attack.</summary>
    public static Ff7BattleAiCounterPlan SelectCounter(Ff7BattleAiActor target, int attackerSlot, ushort hitMask, ushort typedHitMask, ushort deathMask, ushort suppressedMask, ushort processedMask, ushort attackFlags)
    {
        ArgumentNullException.ThrowIfNull(target);
        ushort targetBit = SlotBit(target.Slot);
        ushort sourceBit = SlotBit(attackerSlot);
        if ((suppressedMask & targetBit) != 0)
        {
            return new(target, processedMask, []);
        }
        if ((deathMask & targetBit) != 0)
        {
            if ((target.Conditions & 0x2000) != 0)
            {
                return new(target with { LastAttackerMask = sourceBit }, processedMask, []);
            }
            ushort lastAttacker = attackerSlot == target.Slot ? (ushort)0 : sourceBit;
            return new(target with { Conditions = target.Conditions | 0x2000, LastAttackerMask = lastAttacker }, processedMask, [3]);
        }
        if ((hitMask & targetBit) == 0 || (processedMask & targetBit) != 0 || (target.Statuses & 0x02404404) != 0)
        {
            return new(target, processedMask, []);
        }

        byte[] callbacks = [2];
        if ((typedHitMask & targetBit) != 0)
        {
            byte typed = (attackFlags & 4) != 0 ? (byte)5 : (byte)4;
            callbacks = [2, typed];
        }
        return new(target with { LastAttackerMask = sourceBit }, (ushort)(processedMask | targetBit), callbacks);
    }

    private static ushort SlotBit(int slot)
    {
        if (slot is < 0 or 3 or > 9)
        {
            throw new InvalidDataException("FFVII battle actor mask requires a physical participant slot.");
        }
        return (ushort)(1 << slot);
    }
}
