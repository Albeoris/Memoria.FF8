using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGuestItemInventoryTests
{
    [Fact]
    public void Enemy_recipient_elixir_keeps_one_source_debit_across_cold_restore()
    {
        NativeItemStock[] slots = Enumerable.Repeat(new NativeItemStock(0, 0), 198).ToArray();
        slots[4] = new(9, 2);
        GameplaySaveEconomy origin = new(slots, null, 0, 0, 0, 0);
        Ff7BattleGuestItemSelection use = new(1, 10, 0, 4, 9);
        CrossoverGuestItemInventory pending = CrossoverGuestItemInventory.Begin(origin).Reserve(use);
        Assert.True(CrossoverGuestItemInventory.Supports(9));
        Assert.Equal(1, pending.Slots.Single(stock => stock.ItemId == 9).Count);
        CrossoverGuestItemInventory restored = JsonSerializer.Deserialize<CrossoverGuestItemInventory>(JsonSerializer.Serialize(pending))!.Restore(origin, 1, [0], 10);
        Assert.Equal(use, Assert.Single(restored.AllPending).Selection);
        CrossoverGuestItemInventory completed = restored.Complete(0, 4, 9, refund: false);
        CrossoverGuestItemInventory cold = JsonSerializer.Deserialize<CrossoverGuestItemInventory>(JsonSerializer.Serialize(completed))!.Restore(origin, 1, [0], 11);
        Assert.Equal(1, cold.Slots.Single(stock => stock.ItemId == 9).Count);
        Assert.Equal(use, Assert.Single(cold.Ledger).Selection);
        Assert.Empty(cold.AllPending);
        Assert.Throws<InvalidDataException>(() => cold.Complete(0, 4, 9, refund: false));
    }

    [Fact]
    public void RestoreReplaysSharedReservationsInTheirOriginalOrder()
    {
        NativeItemStock[] slots = Enumerable.Repeat(new NativeItemStock(0, 0), 198).ToArray();
        slots[4] = new(1, 1);
        slots[8] = new(7, 1);
        GameplaySaveEconomy origin = new(slots, null, 0, 0, 0, 0);
        Ff7BattleGuestItemSelection first = new(1, 10, 0, 1, 1);
        Ff7BattleGuestItemSelection second = new(1, 11, 1, 0, 7);
        CrossoverGuestItemInventory inventory = CrossoverGuestItemInventory.Begin(origin).Reserve(first).Reserve(second);
        Assert.Throws<InvalidOperationException>(() => inventory.Reserve(new(1, 12, 2, 2, 7)));

        inventory = inventory.Complete(1, 0, 7, refund: true);
        CrossoverGuestItemInventory saved = JsonSerializer.Deserialize<CrossoverGuestItemInventory>(JsonSerializer.Serialize(inventory))!;
        CrossoverGuestItemInventory restored = saved.Restore(origin, 1, [0, 1], 12);
        Assert.Equal(first, Assert.Single(restored.AllPending).Selection);
        Assert.Equal(second, Assert.Single(restored.Ledger).Selection);

        inventory = inventory.Complete(0, 1, 1, refund: true);
        restored = restored.Complete(0, 1, 1, refund: true);
        Assert.Equal(inventory.Slots, restored.Slots);
        Assert.Equal(new NativeItemStock(7, 1), restored.Slots[0]);
        Assert.Equal(new NativeItemStock(1, 1), restored.Slots[1]);
        Assert.Equal(new long[] { 2, 1 }, restored.Ledger.Select(debit => debit.Ordinal));
        Assert.Empty(restored.AllPending);
    }

    [Fact]
    public void RestoreAcceptsLegacyItemReceiptsWithoutSequenceFields()
    {
        NativeItemStock[] slots = Enumerable.Repeat(new NativeItemStock(0, 0), 198).ToArray();
        slots[4] = new(1, 2);
        GameplaySaveEconomy origin = new(slots, null, 0, 0, 0, 0);
        Ff7BattleGuestItemSelection use = new(1, 10, 0, 1, 1);
        Ff7BattleGuestItemSelection queued = new(1, 11, 0, 0, 1);
        CrossoverGuestItemInventory old = new(Enumerable.Repeat(new NativeItemStock(0, 0), 198).ToArray(), [new(1, use, 4)], new(2, queued, 4));

        CrossoverGuestItemInventory restored = old.Restore(origin, 1, 0, 11);

        Assert.Equal(old.Slots, restored.Slots);
        Assert.Equal(2, Assert.Single(restored.Ledger).CompletedSequence);
        Assert.Equal(3, Assert.Single(restored.AllPending).ReservedSequence);
    }
}
