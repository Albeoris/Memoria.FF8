using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverGuestItemDebit(long Ordinal, Ff7BattleGuestItemSelection Selection, int OriginSlot, bool Refunded = false)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long ReservedSequence { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long CompletedSequence { get; init; }
}

/// <summary>The FFVIII battle's own physical stock rows and ordered command reservations.</summary>
internal sealed record CrossoverGuestItemInventory(NativeItemStock[] Slots, ImmutableArray<CrossoverGuestItemDebit> Ledger, CrossoverGuestItemDebit? Pending = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ImmutableArray<CrossoverGuestItemDebit> OtherPending { get; init; }

    [JsonIgnore]
    public IEnumerable<CrossoverGuestItemDebit> AllPending => Pending is not { } first ? [] : new[] { first }.Concat(OtherPending.IsDefault ? [] : OtherPending);

    public CrossoverGuestItemDebit? PendingFor(int actor) => AllPending.SingleOrDefault(debit => debit.Selection.ActorSlot == actor);

    public static CrossoverGuestItemInventory Begin(GameplaySaveEconomy source)
    {
        NativeBattleItemStockRules.Validate(source.Items);
        return new(source.Items.ToArray(), []);
    }

    public CrossoverGuestItemInventory Reserve(Ff7BattleGuestItemSelection selection)
    {
        if (PendingFor(selection.ActorSlot) is not null)
        {
            throw new InvalidOperationException("This guest already owns a queued item reservation.");
        }
        CrossoverGuestItemDebit[] pending = AllPending.ToArray();
        NativeItemStock[] slots = Slots.ToArray();
        int row = NativeBattleItemStockRules.Consume(slots, selection.ItemId);
        CrossoverGuestItemDebit reserved = new(Ledger.Length + pending.Length + 1, selection, row) { ReservedSequence = NextSequence(pending.Length) };
        return WithPending(this with { Slots = slots }, [.. pending, reserved]);
    }

    public CrossoverGuestItemInventory Complete(int target, ushort item, bool refund)
        => Complete(Pending?.Selection.ActorSlot ?? -1, target, item, refund);

    public CrossoverGuestItemInventory Complete(int actor, int target, ushort item, bool refund)
    {
        if (PendingFor(actor) is not { } pending || pending.Selection.TargetSlot != target || pending.Selection.ItemId != item)
        {
            throw new InvalidDataException("The guest item queue no longer owns its original physical debit.");
        }
        NativeItemStock[] slots = Slots;
        if (refund)
        {
            slots = Slots.ToArray();
            if (NativeBattleItemStockRules.Add(slots, item, 1) != 1)
            {
                throw new InvalidDataException("The original item refund could not return its reserved unit.");
            }
        }
        CrossoverGuestItemDebit[] remaining = AllPending.Where(debit => debit != pending).ToArray();
        return WithPending(this with { Slots = slots, Ledger = Ledger.Add(pending with { Refunded = refund, CompletedSequence = NextSequence(remaining.Length + 1) }) }, remaining);
    }

    public CrossoverGuestItemInventory Restore(GameplaySaveEconomy source, long request, int actor, long tick)
        => Restore(source, request, [actor], tick);

    public CrossoverGuestItemInventory Restore(GameplaySaveEconomy source, long request, IReadOnlyCollection<int> actors, long tick)
    {
        if (Ledger.IsDefault || Pending is null && !OtherPending.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Guest items require their original reservation ledger.");
        }
        CrossoverGuestItemDebit[] pending = AllPending.ToArray();
        CrossoverGuestItemDebit[] debits = Ledger.Concat(pending).ToArray();
        if (debits.Any(debit => debit is null) || pending.Any(debit => debit.Refunded || debit.CompletedSequence != 0))
        {
            throw new InvalidDataException("An unfinished guest item reservation cannot already be completed.");
        }

        bool legacy = debits.All(debit => debit.ReservedSequence == 0);
        if (legacy && (pending.Length > 1 || Ledger.Any(debit => debit.CompletedSequence != 0)))
        {
            throw new InvalidDataException("Guest item reservations lack their recorded order.");
        }
        List<(long Sequence, bool Complete, CrossoverGuestItemDebit Debit)> journal = [];
        foreach (CrossoverGuestItemDebit debit in debits)
        {
            journal.Add((legacy ? debit.Ordinal * 2 - 1 : debit.ReservedSequence, false, debit));
        }
        foreach (CrossoverGuestItemDebit debit in Ledger)
        {
            journal.Add((legacy ? debit.Ordinal * 2 : debit.CompletedSequence, true, debit));
        }

        CrossoverGuestItemInventory expected = Begin(source);
        long sequence = 0;
        foreach ((long operation, bool complete, CrossoverGuestItemDebit debit) in journal.OrderBy(entry => entry.Sequence))
        {
            Ff7BattleGuestItemSelection selection = debit.Selection;
            if (operation != ++sequence || selection.RequestId != request || !actors.Contains(selection.ActorSlot) || selection.SubmittedAt < 0 || selection.SubmittedAt > tick || selection.TargetSlot is < 0 or >= 3 || !Supports(selection.ItemId))
            {
                throw new InvalidDataException("The guest item receipt belongs to a different queue occurrence or origin item.");
            }
            if (complete)
            {
                if (expected.PendingFor(selection.ActorSlot)?.Ordinal != debit.Ordinal)
                {
                    throw new InvalidDataException("A completed guest item does not own its recorded reservation.");
                }
                expected = expected.Complete(selection.ActorSlot, selection.TargetSlot, selection.ItemId, debit.Refunded);
            }
            else
            {
                if (expected.PendingFor(selection.ActorSlot) is not null)
                {
                    throw new InvalidDataException("A guest has multiple pending item reservations.");
                }
                expected = expected.Reserve(selection);
                CrossoverGuestItemDebit reserved = expected.PendingFor(selection.ActorSlot)!;
                if (reserved.Ordinal != debit.Ordinal || reserved.OriginSlot != debit.OriginSlot)
                {
                    throw new InvalidDataException("The guest item receipt changed its ordinal or physical origin stock row.");
                }
            }
        }
        if (!expected.Slots.SequenceEqual(Slots))
        {
            throw new InvalidDataException("Guest item stock disagrees with the exact origin debits and refunds.");
        }
        return expected;
    }

    private int NextSequence(int pendingCount) => checked(Ledger.Length * 2 + pendingCount + 1);

    private static CrossoverGuestItemInventory WithPending(CrossoverGuestItemInventory inventory, CrossoverGuestItemDebit[] pending)
        => inventory with { Pending = pending.FirstOrDefault(), OtherPending = pending.Length <= 1 ? default : pending.Skip(1).ToImmutableArray() };

    public static bool Supports(ushort item) => item is 1 or 7 or 16;
}
