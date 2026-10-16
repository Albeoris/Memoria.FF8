namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private readonly NativeItemStock[] nativeItems = Enumerable.Repeat(new NativeItemStock(0, 0), 198).ToArray();

    private static NativeItemStock[] ExpandItemStocks(IEnumerable<BattleStock> stocks)
    {
        NativeItemStock[] slots = Enumerable.Repeat(new NativeItemStock(0, 0), 198).ToArray();
        int index = 0;
        foreach (BattleStock stock in stocks)
        {
            slots[index++] = new NativeItemStock(checked((byte)stock.Id), checked((byte)stock.Count));
        }
        return slots;
    }

    private void RestoreNativeItems(NativeItemStock[] slots)
    {
        ValidateNativeItemInventory(slots);
        Array.Copy(slots, nativeItems, 198);
    }

    internal static void ValidateNativeItemInventory(NativeItemStock[] slots)
    {
        if (slots is not { Length: 198 } || slots.Any(stock => stock is null || stock.Count > 100 || (stock.ItemId == 0) != (stock.Count == 0)) || slots.Where(stock => stock.ItemId != 0).Select(stock => stock.ItemId).Distinct().Count() != slots.Count(stock => stock.ItemId != 0))
        {
            throw new InvalidDataException("Native battle item inventory requires198 physical slots with unique nonempty items.");
        }
    }

    private int ConsumeNativeItem(int id)
    {
        int index = Array.FindIndex(nativeItems, stock => stock.ItemId == id && stock.Count > 0);
        if (index < 0)
        {
            throw new InvalidOperationException("The selected battle item stock is exhausted.");
        }
        NativeItemStock stock = nativeItems[index];
        nativeItems[index] = stock.Count == 1 ? new NativeItemStock(0, 0) : stock with { Count = (byte)(stock.Count - 1) };
        return index;
    }

    private int AddNativeBattleItem(int id, int count)
    {
        if (id == 0 || count <= 0)
        {
            return 0;
        }
        int index = Array.FindIndex(nativeItems, stock => stock.ItemId == id);
        if (index < 0)
        {
            index = Array.FindIndex(nativeItems, stock => stock.ItemId == 0);
        }
        if (index < 0 || count <= 0)
        {
            return 0;
        }
        int added = Math.Min(100 - nativeItems[index].Count, count);
        if (added > 0)
        {
            nativeItems[index] = new NativeItemStock(checked((byte)id), checked((byte)(nativeItems[index].Count + added)));
        }
        return added;
    }

    private void CompleteNativeActionInventory(BattleCombatant actor, BattleActionPresentation command)
    {
        //485610 consumes ordinary player magic after its effects; Angel Wing and Draw-cast remain free.
        if (actor.IsPlayer && command.NativeMagicTargets.IsDefaultOrEmpty && command.Ability != BattleAbility.Draw && !command.FreeNativeMagic && (command.Ability == BattleAbility.Magic || command.NativeAction?.Command == 2))
        {
            actor.TryConsumeMagic(command.NativeAction?.ActionId ?? command.ActionId);
        }
        ReprojectNativePlayerStatistics(actor);
    }

    private void RefundNativeReservedItem(int slot, int id)
    {
        if ((uint)slot >= nativeItems.Length)
        {
            throw new InvalidDataException("Native item reservation has no original physical slot.");
        }
        //485DC0/485EC0 refund through486B40: existing matching stock, then first empty slot.
        AddNativeBattleItem(id, 1);
    }

    private void RefundCancelledNativeItem(BattleAttackCommand command)
    {
        if (command.NativeHeaderIndex is null && command.NativePlayerReaction is null && command.ReservedNativeItemSlot is int slot)
        {
            RefundNativeReservedItem(slot, command.NativeAction?.ActionId ?? command.ActionId);
        }
    }

    private void RefundCancelledNativeItem(BattleActionPresentation command)
    {
        if (command.NativeHeaderIndex is null && command.NativePlayerReaction is null && command.Phase == BattleActionPhase.Windup && command.ReservedNativeItemSlot is int slot)
        {
            RefundNativeReservedItem(slot, command.NativeAction?.ActionId ?? command.ActionId);
        }
    }

    private static void ValidateNativeActionInventory(BattleCombatant actor, BattleAbility ability, BattleNativeAction? native, int? reservedSlot, bool freeMagic)
    {
        if (reservedSlot is int slot && ((uint)slot >= 198 || ability != BattleAbility.Item && native?.Command != 4))
        {
            throw new InvalidDataException("Saved item reservation does not identify its physical inventory slot and item command.");
        }
        if (freeMagic && (!actor.IsPlayer || ability != BattleAbility.Magic && native?.Command != 2))
        {
            throw new InvalidDataException("Saved free magic does not belong to a player's compelled spell command.");
        }
    }
}
