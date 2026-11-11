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

    internal static void ValidateNativeItemInventory(NativeItemStock[] slots) => NativeBattleItemStockRules.Validate(slots);

    private int ConsumeNativeItem(int id) => NativeBattleItemStockRules.Consume(nativeItems, id);

    private int AddNativeBattleItem(int id, int count)
    {
        int added = NativeBattleItemStockRules.Add(nativeItems, id, count);
        if (added > 0 && ChocoboRewardCatalog.FindItem(id) is { } reward)
        {
            chocoboRewards = chocoboRewards with { AcquiredUniqueRewardIds = chocoboRewards.AcquiredUniqueRewardIds.Add(reward.Id) };
        }
        return added;
    }

    private bool CompleteNativeActionInventory(BattleCombatant actor, BattleActionPresentation command, bool silencedMagic)
    {
        if (silencedMagic)
        {
            ReleaseMagicReservation(command.MagicReservationId);
            return false;
        }
        bool continueAffinity = true;
        //485610 consumes ordinary player magic after its effects; Angel Wing and Draw-cast remain free.
        if (actor.IsPlayer && command.NativeMagicTargets.IsDefaultOrEmpty && command.Ability != BattleAbility.Draw && !command.FreeNativeMagic && (command.Ability == BattleAbility.Magic || command.NativeAction?.Command == 2))
        {
            int spell = command.NativeAction?.ActionId ?? command.ActionId;
            //486A10 returns FF on the last debit;485610 then skips the affinity loop.
            if (command.MagicReservationId is not null)
            {
                CommitMagicReservation(command.MagicReservationId);
                continueAffinity = magicVitals?.MagicSystem.InfiniteMagic == true || actor.MagicStocks.Any(stock => stock.Id == spell && stock.Count > 0);
            }
            else
            {
                continueAffinity = UnlimitedMagic || actor.TryConsumeMagic(spell) && actor.NativeMagicInventory.Any(stock => stock.SpellId == spell && stock.Count > 0);
            }
        }
        ReprojectNativePlayerStatistics(actor);
        return continueAffinity;
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
        ReleaseMagicReservation(command.MagicReservationId);
        if (command.NativeHeaderIndex is null && command.NativePlayerReaction is null && command.ReservedNativeItemSlot is int slot)
        {
            RefundNativeReservedItem(slot, command.NativeAction?.ActionId ?? command.ActionId);
        }
    }

    private void RefundCancelledNativeItem(BattleActionPresentation command)
    {
        ReleaseMagicReservation(command.MagicReservationId);
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
        bool reflectedMagic = ability == BattleAbility.Magic && native?.Command == 247;
        if (freeMagic && !reflectedMagic && (!actor.IsPlayer || ability != BattleAbility.Magic && native?.Command != 2))
        {
            throw new InvalidDataException("Saved free magic requires a player compelled spell or a native reflected spell command.");
        }
    }
}
