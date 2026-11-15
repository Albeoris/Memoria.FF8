using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Maps original target words to deferred roster slots and preserves their packet representation.</summary>
internal static class NativeBattleTargetRequestAdapter
{
    private const int RandomFlag = 0x2000;
    private const int KnockedOutAllyFlag = 0x4000;
    private const int GroupFlag = 0x8000;
    private const int LastSelectionPosition = 12;

    internal enum ReservedPosition
    {
        Seven = 7,
        Eight,
        Nine,
        Ten,
        Eleven,
        Twelve,
        Thirteen,
        Fourteen,
        Fifteen
    }

    internal sealed record DeferredOperands(ImmutableArray<BattleRosterSlot> Slots, ImmutableArray<ReservedPosition> ReservedPositions) : BattleTargetSelection
    {
        internal bool IsEmpty => Slots.IsEmpty && ReservedPositions.IsEmpty;
        internal BattleSide Side => ReservedPositions.IsEmpty && Slots.All(slot => slot.Side == BattleSide.Allies) ? BattleSide.Allies : BattleSide.Enemies;
    }

    internal sealed record OriginalWordProjection(DeferredOperands Operands, bool Group = false, bool Random = false)
    {
        internal bool RequiresOriginalSlots => !Group && !Random && !Operands.IsEmpty;

        internal BattleTargetSelection DecodeSelection(bool samplesOriginalSlots)
        {
            if (Random)
            {
                return new BattleTargetSelection.RandomOnSide(Operands.Side, samplesOriginalSlots);
            }
            if (Group)
            {
                bool both = Operands.Slots.Length == NativeBattleSlots.Count && Operands.ReservedPositions is [ReservedPosition.Seven];
                return new BattleTargetSelection.SideGroup(both ? BattleTargetSide.Both : ToTargetSide(Operands.Side));
            }
            return Operands;
        }

        internal ushort WriteOriginalWord(bool allowsKnockedOutAllies)
        {
            int modifiers = (Group ? GroupFlag : 0) | (Random ? RandomFlag : 0) | (allowsKnockedOutAllies ? KnockedOutAllyFlag : 0);
            return checked((ushort)(EncodePositions(Positions(Operands)) | modifiers));
        }
    }

    internal sealed record OriginalPreviousOperand(int Operand) : BattlePreviousTarget;

    internal static DeferredOperands EmptyOriginalSelection { get; } = new([], []);

    internal static DeferredOperands SelectOriginalSlot(BattleRosterSlot slot)
    {
        return new([slot], []);
    }

    internal static OriginalWordProjection OriginalGroup(BattleTargetSide side, IEnumerable<BattleRosterSlot>? authoredSlots = null)
    {
        if (authoredSlots is not null)
        {
            return new(SelectOriginalSlots(authoredSlots), Group: true);
        }
        IEnumerable<BattleRosterSlot> slots = Enumerable.Range(0, NativeBattleSlots.Count).Select(NativeBattleSlots.Decode);
        return new(new(slots.Where(slot => side == BattleTargetSide.Both || ToTargetSide(slot.Side) == side).ToImmutableArray(), side == BattleTargetSide.Allies ? [] : [ReservedPosition.Seven]), Group: true);
    }

    internal static DeferredOperands SelectOriginalSlots(IEnumerable<BattleRosterSlot> slots)
    {
        return new(slots.Distinct().OrderBy(slot => NativeBattleSlots.Encode(slot)).ToImmutableArray(), []);
    }

    internal static (OriginalWordProjection Projection, bool AllowsKnockedOutAllies) DecodeAiActorOperand(int operand)
    {
        // Native SHL uses the low five shift bits, then the interpreter retains only the low WORD.
        int position = operand & 31;
        return position switch
        {
            < NativeBattleSlots.Count => (new(SelectOriginalSlot(NativeBattleSlots.Decode(position))), false),
            <= LastSelectionPosition => (new(new([], [(ReservedPosition)position])), false),
            13 => (new(EmptyOriginalSelection, Random: true), false),
            14 => (new(EmptyOriginalSelection), true),
            15 => (new(EmptyOriginalSelection, Group: true), false),
            _ => (new(EmptyOriginalSelection), false)
        };
    }

    internal static BattleTargetSide ToTargetSide(BattleSide side) => side == BattleSide.Allies ? BattleTargetSide.Allies : BattleTargetSide.Enemies;

    internal static BattleTargetRequest CreatePlayerRequest(byte flags, int nativeIndex)
    {
        if (nativeIndex is < 0 or >= 16)
        {
            throw new ArgumentOutOfRangeException(nameof(nativeIndex));
        }
        (OriginalWordProjection original, bool allowsKo) = DecodeAiActorOperand(nativeIndex);
        original = (flags & 0x30) switch
        {
            0 => original with { Group = true },
            0x10 => original,
            0x20 => OriginalGroup(BattleTargetSide.Both),
            _ => throw new NotSupportedException("Original player target flags request an unresolved target mode.")
        };
        if ((flags & 0x80) != 0 && !original.Operands.ReservedPositions.Contains(ReservedPosition.Seven))
        {
            original = original with { Operands = original.Operands with { ReservedPositions = original.Operands.ReservedPositions.Insert(0, ReservedPosition.Seven) } };
        }
        return CreateOriginalRequest(original with { Random = original.Random || (flags & 2) != 0 }, BattleTargetEligibility.SavedStatus,
            (allowsKo && (flags & 0x30) != 0x20) || (flags & 1) != 0);
    }

    internal static BattleTargetSelection RandomizeOriginalSelection(BattleTargetSelection selection, bool samplesOriginalSlots)
    {
        BattleSide side = selection switch
        {
            DeferredOperands operands => operands.Side,
            BattleTargetSelection.SideGroup group => group.Side == BattleTargetSide.Allies ? BattleSide.Allies : BattleSide.Enemies,
            _ => throw new ArgumentException("Randomizing an original target requires decoded original selection operands.", nameof(selection))
        };
        return new BattleTargetSelection.RandomOnSide(side, samplesOriginalSlots);
    }

    internal static DeferredOperands CaptureMagicPreflight(ushort originalWord)
    {
        return DecodePositions(Enumerable.Range(0, LastSelectionPosition + 1).Where(position => (originalWord & (1 << position)) != 0));
    }

    internal static BattleActorReference? ReadFirstActiveMagicAnchor(DeferredOperands operands, Func<BattleRosterSlot, BattleActorReference?> readOccupant, Func<BattleActorReference, bool> isActive)
    {
        foreach (BattleRosterSlot slot in operands.Slots)
        {
            if (readOccupant(slot) is { } actor && isActive(actor))
            {
                return actor;
            }
        }
        return null;
    }

    internal static BattlePreviousTarget CapturePrevious(ushort word)
    {
        return CapturePrevious(DecodeProjection(word), (word & KnockedOutAllyFlag) != 0);
    }

    internal static BattlePreviousTarget CapturePrevious(OriginalWordProjection selection, bool allowsKnockedOutAllies)
    {
        int position = Positions(selection.Operands).FirstOrDefault(-1);
        return new OriginalPreviousOperand(position >= 0 ? position : selection.Random ? 13 : allowsKnockedOutAllies ? 14 : selection.Group ? 15 : 32);
    }

    internal static BattleActorReference ResolvePrevious(OriginalPreviousOperand target, Func<BattleRosterSlot, BattleActorReference?> readOccupant)
    {
        if (!NativeBattleSlots.Contains(target.Operand))
        {
            throw new NotSupportedException("The retained original previous target leaves its participant bank.");
        }
        return readOccupant(NativeBattleSlots.Decode(target.Operand)) ?? throw new NotSupportedException("The retained original previous target has no current participant.");
    }

    private static OriginalWordProjection DecodeProjection(ushort word)
    {
        return new(CaptureMagicPreflight(word), (word & GroupFlag) != 0, (word & RandomFlag) != 0);
    }

    internal static BattleTargetRequest Decode(ushort word, BattleTargetEligibility eligibility, BattleTargetReplacement replacement, bool samplesOriginalSlots, BattleActorReference? deathCallbackSelf = null)
    {
        return CreateOriginalRequest(DecodeProjection(word), eligibility, (word & KnockedOutAllyFlag) != 0, samplesOriginalSlots, replacement, deathCallbackSelf);
    }

    internal static BattleTargetRequest CreateOriginalRequest(OriginalWordProjection projection, BattleTargetEligibility eligibility, bool allowsKnockedOutAllies, bool samplesOriginalSlots = true, BattleTargetReplacement replacement = BattleTargetReplacement.UseCurrentBattlePolicy, BattleActorReference? deathCallbackSelf = null)
    {
        return new(projection.DecodeSelection(samplesOriginalSlots), eligibility, allowsKnockedOutAllies, replacement, deathCallbackSelf) { OriginalProjection = projection };
    }

    internal static ushort? ProjectOriginalWord(BattleTargetRequest request, Func<BattleActorReference, BattleRosterSlot?> readSlot)
    {
        return ProjectSelection(request, readSlot)?.WriteOriginalWord(request.AllowsKnockedOutAllies);
    }

    internal static BattlePreviousTarget CapturePrevious(BattleTargetRequest request, Func<BattleActorReference, BattleRosterSlot?> readSlot)
    {
        OriginalWordProjection projection = ProjectSelection(request, readSlot) ?? throw new InvalidDataException("An original previous target requires a representable roster slot.");
        return CapturePrevious(projection, request.AllowsKnockedOutAllies);
    }

    private static OriginalWordProjection? ProjectSelection(BattleTargetRequest request, Func<BattleActorReference, BattleRosterSlot?> readSlot)
    {
        if (request.OriginalProjection is { } projection)
        {
            return projection;
        }
        switch (request.Selection)
        {
            case BattleTargetSelection.None:
                return new(EmptyOriginalSelection);
            case DeferredOperands operands:
                return new(operands);
            case BattleTargetSelection.SideGroup group:
                OriginalWordProjection sideGroup = OriginalGroup(group.Side);
                if (group.Side == BattleTargetSide.Enemies)
                {
                    sideGroup = sideGroup with { Operands = sideGroup.Operands with { ReservedPositions = [] } };
                }
                return sideGroup;
            case BattleTargetSelection.RandomOnSide random:
                return new(OriginalGroup(ToTargetSide(random.Side)).Operands with { ReservedPositions = [] }, Random: true);
            case BattleTargetSelection.Explicit exact:
                var slots = ImmutableArray.CreateBuilder<BattleRosterSlot>();
                foreach (BattleActorReference reference in exact.Actors)
                {
                    if (readSlot(reference) is not { } slot || NativeBattleSlots.TryEncode(slot) is null)
                    {
                        return null;
                    }
                    slots.Add(slot);
                }
                return new(SelectOriginalSlots(slots));
            default:
                throw new InvalidDataException("Battle target request has no original transport encoding.");
        }
    }

    internal static ImmutableArray<BattleActorReference> ReadAuthoredTargets(DeferredOperands operands, Func<BattleRosterSlot, BattleActorReference?> readOccupant)
    {
        if (!operands.ReservedPositions.IsEmpty)
        {
            throw new NotSupportedException("A reached original target selection leaves its participant bank.");
        }
        return operands.Slots.Select(slot => readOccupant(slot) ?? throw new NotSupportedException("A reached original target slot has no current participant.")).ToImmutableArray();
    }

    internal static bool MaySelectOriginalSlot(BattleTargetRequest request, BattleRosterSlot slot)
    {
        return request.Selection is DeferredOperands operands && operands.Slots.Contains(slot);
    }

    internal static bool HasNonActorSelectors(BattleTargetRequest request)
    {
        return request.Selection switch
        {
            DeferredOperands operands => !operands.ReservedPositions.IsEmpty || request.AllowsKnockedOutAllies,
            BattleTargetSelection.SideGroup or BattleTargetSelection.RandomOnSide => true,
            _ => request.AllowsKnockedOutAllies
        };
    }

    internal static bool IsOriginalSelectionAdmitted(DeferredOperands operands, Func<BattleRosterSlot, BattleActorReference?> readOccupant, Func<BattleActorReference, bool> isAvailable, bool allowReplacement)
    {
        return operands.IsEmpty ||
            operands.Slots.Any(slot => readOccupant(slot) is { } actor && (!allowReplacement || isAvailable(actor))) ||
            allowReplacement && Enumerable.Range(0, NativeBattleSlots.Count).Select(NativeBattleSlots.Decode)
                .Any(slot => slot.Side == operands.Side && readOccupant(slot) is { } actor && isAvailable(actor));
    }

    internal static (ImmutableArray<BattleActorReference> Targets, bool Unavailable) ResolveDeferred(DeferredOperands operands, Func<BattleRosterSlot, BattleActorReference?> readOccupant, Func<BattleActorReference, bool> isAvailable, bool allowReplacement, Action<int, int, int>? observe = null)
    {
        Dictionary<BattleRosterSlot, BattleActorReference?> occupants = Enumerable.Range(0, NativeBattleSlots.Count).Select(NativeBattleSlots.Decode).ToDictionary(slot => slot, readOccupant);
        DeferredOperands available = SelectOriginalSlots(occupants.Where(entry => entry.Value is { } actor && isAvailable(actor)).Select(entry => entry.Key));
        (DeferredOperands resolved, bool unavailable) = ResolveSelection(operands, available, allowReplacement);
        observe?.Invoke(EncodePositions(Positions(operands)), EncodePositions(Positions(available)), EncodePositions(Positions(resolved)));
        return (ReadAuthoredTargets(resolved, slot => occupants[slot]), unavailable);
    }

    internal static int ResolveObservedOriginalWord(ushort input, ushort available, bool disableAutomaticReplacement)
    {
        OriginalWordProjection original = DecodeProjection(input);
        DeferredOperands availableSlots = CaptureMagicPreflight(available);
        if (original.Group)
        {
            BattleTargetSide side = ((BattleTargetSelection.SideGroup)(original with { Random = false }).DecodeSelection(true)).Side;
            return EncodePositions(availableSlots.Slots.Where(slot => side == BattleTargetSide.Both || ToTargetSide(slot.Side) == side).Select(slot => NativeBattleSlots.Encode(slot)));
        }
        (DeferredOperands resolved, _) = ResolveSelection(original.Operands, availableSlots, !disableAutomaticReplacement);
        return EncodePositions(Positions(resolved));
    }

    private static (DeferredOperands Selection, bool Unavailable) ResolveSelection(DeferredOperands selected, DeferredOperands available, bool allowReplacement)
    {
        ImmutableArray<BattleRosterSlot> matched = selected.Slots.Where(available.Slots.Contains).ToImmutableArray();
        if (!matched.IsEmpty)
        {
            return (new(matched, []), false);
        }
        if (!allowReplacement || selected.IsEmpty)
        {
            return (selected, allowReplacement);
        }

        int[] positions = Positions(selected).ToArray();
        HashSet<int> side = available.Slots.Where(slot => slot.Side == selected.Side).Select(slot => NativeBattleSlots.Encode(slot)).ToHashSet();
        // Original replacement probes all authored positions forward, then backward, by equal offsets.
        for (int shift = 1; shift <= 8; shift++)
        {
            int[] shifted = positions.Select(position => position + shift).Where(position => position < 16).ToArray();
            if (shifted.Any(side.Contains))
            {
                return (DecodePositions(shifted), false);
            }
        }
        for (int shift = 1; shift <= 8; shift++)
        {
            int[] shifted = positions.Select(position => position - shift).Where(position => position >= 0).ToArray();
            if (shifted.Any(side.Contains))
            {
                return (DecodePositions(shifted), false);
            }
        }
        return (selected, true);
    }

    private static IEnumerable<int> Positions(DeferredOperands operands)
    {
        return operands.Slots.Select(slot => NativeBattleSlots.Encode(slot)).Concat(operands.ReservedPositions.Select(position => (int)position));
    }

    private static DeferredOperands DecodePositions(IEnumerable<int> positions)
    {
        return new(positions.Where(NativeBattleSlots.Contains).Select(NativeBattleSlots.Decode).ToImmutableArray(), positions.Where(position => !NativeBattleSlots.Contains(position)).Select(position => (ReservedPosition)position).ToImmutableArray());
    }

    private static int EncodePositions(IEnumerable<int> positions)
    {
        int word = 0;
        foreach (int position in positions)
        {
            word |= 1 << position;
        }
        return word;
    }
}
