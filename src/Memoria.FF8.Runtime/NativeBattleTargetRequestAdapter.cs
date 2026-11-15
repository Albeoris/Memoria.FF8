using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

/// <summary>Decodes original participant selection operands without binding an allocation before dispatch.</summary>
internal static class NativeBattleTargetRequestAdapter
{
    private const ushort SelectionBits = 0x1FFF;
    private const ushort RandomFlag = 0x2000;
    private const ushort KnockedOutAllyFlag = 0x4000;
    private const ushort GroupFlag = 0x8000;
    private const int AllyBits = (1 << NativeBattleSlots.AllyCount) - 1;
    private const int AllBits = (1 << NativeBattleSlots.Count) - 1;

    internal sealed record DeferredOperands : BattleTargetSelection
    {
        internal DeferredOperands(ushort selection)
        {
            if (selection > SelectionBits)
            {
                throw new ArgumentOutOfRangeException(nameof(selection));
            }
            Selection = selection;
        }

        internal ushort Selection { get; }
    }

    internal sealed record OriginalPreviousOperand(int Operand) : BattlePreviousTarget;

    internal static BattlePreviousTarget? CapturePrevious(ushort word)
    {
        return word == 0 ? null : new OriginalPreviousOperand(BitOperations.TrailingZeroCount((uint)word));
    }

    internal static BattleActorReference ResolvePrevious(OriginalPreviousOperand target, Func<BattleRosterSlot, BattleActorReference?> readOccupant)
    {
        if (!NativeBattleSlots.Contains(target.Operand))
        {
            throw new NotSupportedException("The retained original previous target leaves its participant bank.");
        }
        return readOccupant(NativeBattleSlots.Decode(target.Operand)) ?? throw new NotSupportedException("The retained original previous target has no current participant.");
    }

    internal static BattleTargetRequest Decode(ushort word, BattleTargetEligibility eligibility, BattleTargetReplacement replacement, bool samplesOriginalSlots, BattleActorReference? deathCallbackSelf = null)
    {
        ushort selection = (ushort)(word & SelectionBits);
        BattleSide side = selection <= AllyBits ? BattleSide.Allies : BattleSide.Enemies;
        BattleTargetSelection decoded;
        if ((word & RandomFlag) != 0)
        {
            decoded = new BattleTargetSelection.RandomOnSide(side, samplesOriginalSlots);
        }
        else if ((word & GroupFlag) != 0)
        {
            BattleTargetSide selectedSide = selection == byte.MaxValue ? BattleTargetSide.Both : side == BattleSide.Allies ? BattleTargetSide.Allies : BattleTargetSide.Enemies;
            decoded = new BattleTargetSelection.SideGroup(selectedSide);
        }
        else
        {
            decoded = new DeferredOperands(selection);
        }
        return new(decoded, eligibility, (word & KnockedOutAllyFlag) != 0, replacement, deathCallbackSelf);
    }

    internal static (ImmutableArray<BattleActorReference> Targets, bool Unavailable) ResolveDeferred(DeferredOperands operands, Func<BattleRosterSlot, BattleActorReference?> readOccupant, Func<BattleActorReference, bool> isAvailable, bool allowReplacement)
    {
        ushort available = 0;
        BattleActorReference?[] occupants = new BattleActorReference?[NativeBattleSlots.Count];
        for (int index = 0; index < occupants.Length; index++)
        {
            BattleActorReference? actor = readOccupant(NativeBattleSlots.Decode(index));
            occupants[index] = actor;
            if (actor is not null && isAvailable(actor))
            {
                available |= (ushort)(1 << index);
            }
        }
        (int resolved, bool unavailable) = ResolveOriginalWord(operands.Selection, available, allowReplacement);
        if ((resolved & ~AllBits) != 0)
        {
            throw new NotSupportedException("A reached original target selection leaves its participant bank.");
        }
        var selected = ImmutableArray.CreateBuilder<BattleActorReference>();
        for (int index = 0; index < occupants.Length; index++)
        {
            if ((resolved & (1 << index)) != 0)
            {
                selected.Add(occupants[index] ?? throw new NotSupportedException("A reached original target slot has no current participant."));
            }
        }
        return (selected.ToImmutable(), unavailable);
    }

    private static (int Selected, bool Unavailable) ResolveOriginalWord(int selected, int available, bool allowReplacement)
    {
        int matched = selected & available;
        if (matched != 0)
        {
            return (matched, false);
        }
        if (!allowReplacement || selected == 0)
        {
            return (selected, allowReplacement);
        }
        int side = available & (selected <= AllyBits ? AllyBits : AllBits & ~AllyBits);
        for (int shift = 1; shift <= 8; shift++)
        {
            int shifted = selected << shift;
            if ((side & shifted) != 0)
            {
                return (shifted, false);
            }
        }
        for (int shift = 1; shift <= 8; shift++)
        {
            int shifted = selected >> shift;
            if ((side & shifted) != 0)
            {
                return (shifted, false);
            }
        }
        return (selected, true);
    }
}
