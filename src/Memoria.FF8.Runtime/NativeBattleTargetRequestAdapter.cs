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

    internal sealed class OriginalWordProjection
    {
        private readonly ushort word;

        internal OriginalWordProjection(ushort word)
        {
            this.word = word;
        }

        internal ushort WriteOriginalWord()
        {
            return word;
        }
    }

    internal sealed record OriginalPreviousOperand(int Operand) : BattlePreviousTarget;

    internal static DeferredOperands EmptyOriginalSelection { get; } = new(0);

    internal static BattleTargetSelection SelectOriginalSlot(BattleRosterSlot slot)
    {
        return new DeferredOperands(checked((ushort)(1 << NativeBattleSlots.Encode(slot))));
    }

    internal static BattleTargetSelection RandomizeOriginalSelection(BattleTargetSelection selection, bool samplesOriginalSlots)
    {
        BattleSide side = selection switch
        {
            DeferredOperands { Selection: <= AllyBits } or BattleTargetSelection.SideGroup { Side: BattleTargetSide.Allies } => BattleSide.Allies,
            DeferredOperands or BattleTargetSelection.SideGroup => BattleSide.Enemies,
            _ => throw new ArgumentException("Randomizing an original target requires decoded original selection operands.", nameof(selection))
        };
        return new BattleTargetSelection.RandomOnSide(side, samplesOriginalSlots);
    }

    internal static DeferredOperands CaptureMagicPreflight(ushort originalWord)
    {
        return new DeferredOperands((ushort)(originalWord & SelectionBits));
    }

    internal static BattleActorReference? ReadFirstActiveMagicAnchor(DeferredOperands operands, Func<BattleRosterSlot, BattleActorReference?> readOccupant, Func<BattleActorReference, bool> isActive)
    {
        for (int index = 0; index < NativeBattleSlots.Count; index++)
        {
            if ((operands.Selection & (1 << index)) != 0 && readOccupant(NativeBattleSlots.Decode(index)) is { } actor && isActive(actor))
            {
                return actor;
            }
        }
        return null;
    }

    internal static BattlePreviousTarget CapturePrevious(ushort word)
    {
        return new OriginalPreviousOperand(BitOperations.TrailingZeroCount((uint)word));
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
        return new BattleTargetRequest(decoded, eligibility, (word & KnockedOutAllyFlag) != 0, replacement, deathCallbackSelf)
        {
            OriginalProjection = new(word)
        };
    }

    internal static ushort? ProjectOriginalWord(BattleTargetRequest request, Func<BattleActorReference, BattleRosterSlot?> readSlot)
    {
        if (request.OriginalProjection is { } authored)
        {
            return authored.WriteOriginalWord();
        }
        int selection;
        switch (request.Selection)
        {
            case BattleTargetSelection.None:
                selection = 0;
                break;
            case DeferredOperands operands:
                selection = operands.Selection;
                break;
            case BattleTargetSelection.SideGroup group:
                selection = GroupFlag | (group.Side switch
                {
                    BattleTargetSide.Allies => AllyBits,
                    BattleTargetSide.Enemies => AllBits & ~AllyBits,
                    _ => byte.MaxValue
                });
                break;
            case BattleTargetSelection.RandomOnSide random:
                selection = RandomFlag | (random.Side == BattleSide.Allies ? AllyBits : AllBits & ~AllyBits);
                break;
            case BattleTargetSelection.Explicit exact:
                selection = 0;
                foreach (BattleActorReference reference in exact.Actors)
                {
                    if (readSlot(reference) is not { } slot)
                    {
                        return null;
                    }
                    selection |= 1 << NativeBattleSlots.Encode(slot);
                }
                break;
            default:
                throw new InvalidDataException("Battle target request has no original transport encoding.");
        }
        return checked((ushort)(selection | (request.AllowsKnockedOutAllies ? KnockedOutAllyFlag : 0)));
    }

    internal static ImmutableArray<BattleActorReference> ReadAuthoredTargets(DeferredOperands operands, Func<BattleRosterSlot, BattleActorReference?> readOccupant)
    {
        if ((operands.Selection & ~AllBits) != 0)
        {
            throw new NotSupportedException("A reached original target selection leaves its participant bank.");
        }
        var targets = ImmutableArray.CreateBuilder<BattleActorReference>();
        for (int index = 0; index < NativeBattleSlots.Count; index++)
        {
            if ((operands.Selection & (1 << index)) != 0)
            {
                targets.Add(readOccupant(NativeBattleSlots.Decode(index)) ?? throw new NotSupportedException("A reached original target slot has no current participant."));
            }
        }
        return targets.ToImmutable();
    }

    internal static bool MaySelectOriginalSlot(BattleTargetRequest request, BattleRosterSlot slot)
    {
        return request.Selection is DeferredOperands operands && (operands.Selection & (1 << NativeBattleSlots.Encode(slot))) != 0;
    }

    internal static bool HasNonActorSelectors(BattleTargetRequest request)
    {
        return request.Selection switch
        {
            DeferredOperands operands => (operands.Selection & ~AllBits) != 0 || request.AllowsKnockedOutAllies,
            BattleTargetSelection.SideGroup or BattleTargetSelection.RandomOnSide => true,
            _ => request.AllowsKnockedOutAllies
        };
    }

    internal static bool IsOriginalSelectionAdmitted(DeferredOperands operands, Func<BattleRosterSlot, BattleActorReference?> readOccupant, Func<BattleActorReference, bool> isAvailable, bool allowReplacement)
    {
        if (operands.Selection == 0)
        {
            return true;
        }
        bool hasAvailableOnSide = false;
        BattleSide side = operands.Selection <= AllyBits ? BattleSide.Allies : BattleSide.Enemies;
        for (int index = 0; index < NativeBattleSlots.Count; index++)
        {
            BattleRosterSlot slot = NativeBattleSlots.Decode(index);
            if (readOccupant(slot) is not { } actor)
            {
                continue;
            }
            bool selected = (operands.Selection & (1 << index)) != 0;
            if (selected && (!allowReplacement || isAvailable(actor)))
            {
                return true;
            }
            hasAvailableOnSide |= slot.Side == side && isAvailable(actor);
        }
        return allowReplacement && hasAvailableOnSide;
    }

    internal static (ImmutableArray<BattleActorReference> Targets, bool Unavailable) ResolveDeferred(DeferredOperands operands, Func<BattleRosterSlot, BattleActorReference?> readOccupant, Func<BattleActorReference, bool> isAvailable, bool allowReplacement, Action<int, int, int>? observe = null)
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
        observe?.Invoke(operands.Selection, available, resolved);
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

    internal static int ResolveObservedOriginalWord(ushort input, ushort available, bool disableAutomaticReplacement)
    {
        int selected = input & SelectionBits;
        if ((input & GroupFlag) != 0)
        {
            return available & (selected == byte.MaxValue ? AllBits : selected <= AllyBits ? AllyBits : AllBits & ~AllyBits);
        }
        return ResolveOriginalWord(selected, available, !disableAutomaticReplacement).Selected;
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
            int shifted = unchecked((ushort)(selected << shift));
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
