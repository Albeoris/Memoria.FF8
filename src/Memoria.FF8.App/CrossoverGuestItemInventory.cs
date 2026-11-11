using System.Collections.Immutable;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverGuestItemDebit(long Ordinal, Ff7BattleGuestItemSelection Selection, int OriginSlot, bool Refunded = false);

/// <summary>The FFVIII battle's own physical stock rows and ordered command reservations.</summary>
internal sealed record CrossoverGuestItemInventory(NativeItemStock[] Slots, ImmutableArray<CrossoverGuestItemDebit> Ledger, CrossoverGuestItemDebit? Pending = null)
{
    public static CrossoverGuestItemInventory Begin(GameplaySaveEconomy source)
    {
        NativeBattleItemStockRules.Validate(source.Items);
        return new(source.Items.ToArray(), []);
    }

    public CrossoverGuestItemInventory Reserve(Ff7BattleGuestItemSelection selection)
    {
        if (Pending is not null)
        {
            throw new InvalidOperationException("The guest already owns one queued item reservation.");
        }
        NativeItemStock[] slots = Slots.ToArray();
        int row = NativeBattleItemStockRules.Consume(slots, selection.ItemId);
        return new(slots, Ledger, new(Ledger.Length + 1, selection, row));
    }

    public CrossoverGuestItemInventory Complete(int target, ushort item, bool refund)
    {
        if (Pending is not { } pending || pending.Selection.TargetSlot != target || pending.Selection.ItemId != item)
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
        return new(slots, Ledger.Add(pending with { Refunded = refund }));
    }

    public CrossoverGuestItemInventory Restore(GameplaySaveEconomy source, long request, int actor, long tick)
    {
        if (Ledger.IsDefault)
        {
            throw new InvalidDataException("Guest items require their original reservation ledger.");
        }
        CrossoverGuestItemInventory expected = Begin(source);
        foreach (CrossoverGuestItemDebit debit in Ledger)
        {
            Replay(debit);
            expected = expected.Complete(debit.Selection.TargetSlot, debit.Selection.ItemId, debit.Refunded);
        }
        if (Pending is { } pending)
        {
            Replay(pending);
            if (pending.Refunded)
            {
                throw new InvalidDataException("An unfinished guest item reservation cannot already be refunded.");
            }
        }
        if (!expected.Slots.SequenceEqual(Slots))
        {
            throw new InvalidDataException("Guest item stock disagrees with the exact origin debits and refunds.");
        }
        return expected;

        void Replay(CrossoverGuestItemDebit debit)
        {
            Ff7BattleGuestItemSelection selection = debit.Selection;
            if (debit.Ordinal != expected.Ledger.Length + 1 || selection.RequestId != request || selection.ActorSlot != actor || selection.SubmittedAt < 0 || selection.SubmittedAt > tick || selection.TargetSlot is < 0 or >= 3 || !Supports(selection.ItemId))
            {
                throw new InvalidDataException("The guest item receipt belongs to a different queue occurrence or origin item.");
            }
            expected = expected.Reserve(selection);
            if (expected.Pending!.OriginSlot != debit.OriginSlot)
            {
                throw new InvalidDataException("The guest item receipt changed its physical origin stock row.");
            }
        }
    }

    public static bool Supports(ushort item) => item is 1 or 7 or 16;
}
