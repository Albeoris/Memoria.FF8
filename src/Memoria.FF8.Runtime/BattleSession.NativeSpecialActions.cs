using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private ChocoboRewardLedger chocoboRewards = new();
    private NativeCardInventory? nativeCards;
    private NativeCrtRandomState? nativeCrtRandom;

    private BattleAttackCommand PrepareNativeSpecialCommand(BattleCombatant actor, BattleAttackCommand command)
    {
        if (command.NativeAction?.AttackType is not (17 or 19))
        {
            return command;
        }
        BattleCombatant? target = ResolveNativeActionMask(command.NativeTargetMask).FirstOrDefault();
        if (target is null)
        {
            return command with { NativeSpecial = new(null, null, null, true) };
        }
        BattleNativeEnemySpecial data = target.NativeSpecial ?? throw new NotSupportedException("Card and Devour require their target's original DAT special-action records.");
        if (command.NativeAction.AttackType == 17)
        {
            NativeCardInventory inventory = nativeCards ?? throw new NotSupportedException("Card requires its current saved card inventory, including acquisition bits and capacity.");
            if (data.CommonCard == 255 && data.RareCard == 255)
            {
                return command with { NativeSpecial = new(null, null, null, true) };
            }
            //48FBA0 performs these rolls before48E830 executes the effect, including a guaranteed miss's first roll.
            uint chance = unchecked(256u - 255u * (uint)target.CurrentHp / (uint)target.MaximumHp);
            int cardRoll = NextRandomByte();
            if (chance == 0 || chance < cardRoll)
            {
                return command with { NativeSpecial = new(255, null, null, false) };
            }
            byte selected = NextRandomByte() >= 16 ? data.CommonCard : data.RareCard;
            NativeCardInventory next = inventory.Acquire(selected, out int acquisitionResult);
            nativeCards = next;
            return command with { NativeSpecial = new(selected, null, null, acquisitionResult != 0) };
        }
        if (data.DevourEffects.IsDefault || data.DevourEffects.Length != 3 || target.NativeAi is not { } ai)
        {
            throw new InvalidDataException("Devour requires all three level-band effects and the allocated enemy level band.");
        }
        byte effect = data.DevourEffects[ai.LevelBand];
        bool failed = (NativeSavedStatusBits(target) & 4) != 0 || effect == 255;
        if (!failed && effect >= definition.NativeDevourEffects.Length)
        {
            throw new InvalidDataException("Devour effect index exceeds its original kernel bank.");
        }
        return command with { NativeSpecial = new(null, failed ? null : effect, data.DevourOutcome, failed) };
    }

    private bool TryResolveNativeSpecialHit(BattleCombatant actor, BattleCombatant recipient, BattleActionPresentation command, BattleNativeAction native)
    {
        switch (native.AttackType)
        {
            case 17:
                BattleNativeSpecialContext card = command.NativeSpecial ?? throw new InvalidDataException("Card lacks its original invocation result.");
                if (card.Failed || card.Card is null or 255)
                {
                    if (card.Failed)
                    {
                        QueueNativeSpecialText(actor, recipient, NativeBattleGlobalText(50));
                    }
                    Log("Miss", actor.Id, recipient.Id, 0, "Card failed.");
                    return true;
                }
                string cardName = NativeSpecialName(definition.NativeCardNames, card.Card.Value, "card");
                string separator = NativeBattleGlobalText(11);
                if (separator.Length == 0)
                {
                    throw new InvalidDataException("Native card message separator is empty.");
                }
                QueueNativeSpecialText(actor, recipient, NativeBattleGlobalText(9) + separator[0] + NativeBattleGlobalText(71) + cardName + separator[0] + NativeBattleGlobalText(16));
                CommitNativeEjection(actor, recipient, command, "Card");
                return true;
            case 19:
                BattleNativeSpecialContext devour = command.NativeSpecial ?? throw new InvalidDataException("Devour lacks its original invocation result.");
                if (devour.Failed || devour.DevourEffect is not int effectIndex || recipient.CurrentHp > actor.CurrentHp)
                {
                    if (devour.Failed)
                    {
                        QueueNativeSpecialText(actor, recipient, NativeBattleGlobalText(112));
                    }
                    Log("Miss", actor.Id, recipient.Id, 0, "Devour failed.");
                    return true;
                }
                //4922B0 ->48F0C0 consumes the roll even when equal HP yields a zero success threshold.
                int threshold = 255 * (actor.CurrentHp - recipient.CurrentHp) / actor.CurrentHp;
                int roll = NextRandomByte();
                if (threshold == 0 || threshold < roll)
                {
                    Log("Miss", actor.Id, recipient.Id, 0, "Devour failed.");
                    return true;
                }
                BattleNativeDevourEffect effect = definition.NativeDevourEffects[effectIndex];
                ValidateNativeDevourGrowth(actor, effect);
                QueueNativeSpecialText(actor, recipient, effect.Description ?? "");
                CommitNativeEjection(actor, recipient, command, "Devour");
                ApplyNativeDevourEffect(actor, native, effect);
                return true;
            case 23:
                NativeCrtRandomState crt = nativeCrtRandom ?? throw new NotSupportedException("Angelo Search requires its existing CRT thread random DWORD, independent of battle RNG.");
                NativeCrtRandomState nextCrt = crt.Draw(out ushort crtRoll);
                int item;
                if (definition.ChocoboWorldMode == ChocoboWorldMode.AngeloSearch)
                {
                    nextCrt = nextCrt.Draw(out ushort remasteredItemRoll);
                    item = ChocoboAngeloSearch.Select(crtRoll, remasteredItemRoll, 258 + (definition.RareItem ? 1 : 0));
                }
                else
                {
                    NativeBattleRandomState nextBattleRandom = random.Draw(out byte itemRoll);
                    item = NativeAngeloSearchItem(unchecked((byte)crtRoll), itemRoll);
                    random = nextBattleRandom;
                }
                string itemName = NativeSpecialName(definition.NativeItemNames, item, "item");
                nativeCrtRandom = nextCrt;
                ChocoboRewardDefinition? reward = ChocoboRewardCatalog.FindItem(item);
                bool alreadyAcquired = reward is not null && chocoboRewards.AcquiredUniqueRewardIds.Contains(reward.Id);
                int added = 0;
                if (ChocoboRewardTransactions.TryGrantInventory(nativeItems, chocoboRewards, [new((byte)item, 1)], out NativeItemStock[] awardedItems, out ChocoboRewardLedger awardedLedger, out ImmutableArray<NativeItemStock> delivered))
                {
                    RestoreNativeItems(awardedItems);
                    chocoboRewards = awardedLedger;
                    added = delivered.Sum(stock => stock.Count);
                }
                QueueNativeSpecialText(actor, recipient, added > 0 ? NativeBattleGlobalText(71) + itemName + NativeBattleGlobalText(64) : alreadyAcquired ? $"{itemName}: already obtained." : $"{itemName}: inventory full.");
                RecordNativeActionMemory(actor, recipient, command);
                return true;
            default:
                return false;
        }
    }

    private static int NativeAngeloSearchItem(byte category, byte random)
    {
        //493760 reads the lowBYTE of55CBD2, then one independent48F020 byte in every branch.
        return category switch
        {
            < 0x80 => (random & 7) + 1,
            < 0xA0 => random % 98 + 101,
            < 0xB0 => random % 23 + 9,
            < 0xC0 => random % 34 + 66,
            < 0xC8 => (random & 0x1F) + 33,
            _ => random % 7 + 33
        };
    }

    private static string NativeSpecialName(string?[] names, int id, string family)
    {
        return (uint)id < names.Length && names[id] is string name ? name : throw new NotSupportedException($"Native battle {family} {id} requires its installed localized name.");
    }

    private void QueueNativeSpecialText(BattleCombatant actor, BattleCombatant recipient, string text)
    {
        int frames = 8 * (definition.NativeMessageSpeed ?? throw new NotSupportedException("Native special-action text requires the saved message speed BYTE.")) + 8;
        QueueNativeDescriptorMessage(new BattleEvent(Frame, "NativeText", actor.Id, recipient.Id, frames, text) { NativeMessageMode = 3 });
    }

    private void CommitNativeEjection(BattleCombatant actor, BattleCombatant recipient, BattleActionPresentation command, string source)
    {
        recipient.NativeStatus1 |= 0x10000;
        RecordNativeActionMemory(actor, recipient, command);
        ApplyDamage(actor, recipient, recipient.CurrentHp, source);
    }

    private static void ValidateNativeDevourGrowth(BattleCombatant actor, BattleNativeDevourEffect effect)
    {
        if (effect.Effect == 30 && (effect.StatFlags != 0 || effect.HitPointBonus != 0))
        {
            (actor.NativeGrowth ?? throw new NotSupportedException("Devour growth requires the actual saved character HP and six statistic bonus owners.")).Validate();
        }
    }

    private void ApplyNativeDevourEffect(BattleCombatant actor, BattleNativeAction native, BattleNativeDevourEffect effect)
    {
        // TODO(TD-009): The same completed action owns the native0/10 follow-up until the graphics callback VM replaces scaffold action timing.
        int amount = Math.Min(9999, effect.Quantity * actor.MaximumHp / 16);
        if (effect.Effect == 30)
        {
            RemoveNativeStatuses(actor, effect.SavedStatuses, effect.BattleStatuses);
            if (actor.NativeGrowth is { } growth)
            {
                byte[] bonuses = growth.StatBonuses.ToArray();
                for (int index = 0; index < bonuses.Length; index++)
                {
                    if ((effect.StatFlags & (1 << index)) != 0)
                    {
                        bonuses[index] = (byte)Math.Min(255, bonuses[index] + 1);
                    }
                }
                actor.NativeGrowth = new((ushort)Math.Min(9999, growth.HitPointBonus + effect.HitPointBonus), bonuses.ToImmutableArray());
            }
            ApplyHealing(actor, amount, "Devour");
        }
        else if (effect.Effect == 31)
        {
            ApplyNativeActionStatuses(actor, actor, native with { Power = effect.Quantity, Statuses0 = effect.SavedStatuses, Statuses1 = effect.BattleStatuses, StatusAttack = 200 }, false);
            ApplyDamage(actor, actor, amount, "Devour");
        }
    }

    private void RestoreNativeSpecialOwners(BattleSnapshot snapshot)
    {
        if ((snapshot.NativeCards is null) != (definition.NativeCards is null) || (snapshot.NativeCrtRandom is null) != (definition.NativeCrtRandom is null))
        {
            throw new InvalidDataException("Native special-action state lacks its explicit card inventory or CRT random owner.");
        }
        snapshot.ChocoboRewards.Validate();
        chocoboRewards = snapshot.ChocoboRewards.ObserveInventory(nativeItems);
        nativeCards = snapshot.NativeCards?.DeepCopy();
        nativeCrtRandom = snapshot.NativeCrtRandom;
    }
}
