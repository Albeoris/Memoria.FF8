namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool HasExpandedCombine(BattleCombatant actor)
    {
        return actor.NativeLimit is { Command: 19, InvocationChoices.Length: 2 } &&
            (definition.NativeSavedBattleFlagsKnownMask & 0x30) == 0x30 && (CurrentNativeSavedBattleFlags() & 0x10) == 0 &&
            definition.LimitProgress is { AngeloFlags: >= 0x10 };
    }

    private bool HasExpandedAngelWing(BattleCombatant actor)
    {
        return actor.NativeLimit is { Command: 19, InvocationChoices.Length: 2 } &&
            (definition.NativeSavedBattleFlagsKnownMask & 0x30) == 0x30 && (CurrentNativeSavedBattleFlags() & 0x20) != 0 &&
            IsExpandedAngelWingLimit(FindInstalledNativeAction(19, 4)) && CanCastExpandedAngelWingStock(actor);
    }

    private bool CanCastExpandedAngelWingStock(BattleCombatant actor)
    {
        (NativeMagicStock[] inventory, _, NativeCharacterVitals? owner) = NativeAngelWingInventory(actor);
        IEnumerable<byte> spells = inventory.Select(stock => stock.SpellId);
        if (owner?.MagicSystem.SharedMagicStock == true)
        {
            // Other party members can add Draw stock while Angel Wing owns this actor's commands.
            spells = spells.Concat(enemies.SelectMany(enemy => enemy.NativeDrawSlots).Where(slot => slot.Id < 64).Select(slot => (byte)slot.Id));
        }
        return spells.Distinct().Where(spell => IsNativeAngelWingSpell(actor, spell, owner))
            .All(spell => SupportsExpandedAngelWingSpell(FindInstalledNativeAction(2, spell)));
    }

    private static bool IsExpandedAngelWingLimit(BattleNativeAction action)
    {
        return action is { Command: 19, KernelSection: 25, AttackType: 2, Power: 0, HitCount: 1, TargetFlags: byte flags, Statuses0: 0, Statuses1: 0x2000000, StatusAttack: 255 } &&
            NativeBattleLimitRules.IsFixedAllyTarget(flags) && ExpandedBattleActionRules.SupportsContacts(action);
    }

    private static bool SupportsExpandedCombine(BattleNativeAction action)
    {
        return action is { Command: 19, KernelSection: 25, AttackType: 1 or 2, TargetFlags: byte flags } &&
            (flags & 0xB3) is 0 or 0x10 && (action.Statuses1 & 0x2000000) == 0 && ExpandedBattleActionRules.SupportsContacts(action);
    }

    private BattleCombatant? SelectExpandedCombineTarget(BattleActionDefinition action)
    {
        byte flags = action.NativeAction!.TargetFlags!.Value;
        BattleSide side = (flags & 0x40) == 0 ? BattleSide.Allies : BattleSide.Enemies;
        BattleCombatant[] candidates = Roster(side).Where(candidate => !candidate.Untargetable && HasValidTargetLifeState(action, candidate)).ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }
        return (flags & 0x30) == 0x10 ? candidates[NextRandomByte() % candidates.Length] : candidates[0];
    }

    private void DispatchExpandedAngelWingActors()
    {
        foreach (BattleCombatant actor in allies.Where(actor => actor.IsReady && HasNativeAngelWing(actor) && GetGuardianForm(actor.Id) is null))
        {
            BattleCombatant[] enemies = Roster(BattleSide.Enemies).Where(IsExpandedAiTarget).ToArray();
            if (enemies.Length == 0)
            {
                continue;
            }
            byte? spell = SelectNativeAngelWingSpell(actor);
            if (spell is not byte spellId)
            {
                EnqueueAttack(actor, enemies[NextRandomByte() % enemies.Length]);
                continue;
            }
            BattleNativeAction native = FindInstalledNativeAction(2, spellId);
            if (!SupportsExpandedAngelWingSpell(native))
            {
                throw new NotSupportedException("The selected Angel Wing spell requires a contact contract outside the expanded battle host.");
            }
            byte flags = native.TargetFlags!.Value;
            BattleCombatant target = (flags & 0x30) == 0x10 ? enemies[NextRandomByte() % enemies.Length] : enemies[0];
            byte sequence = NativeBattleActionGraphics.ResolvePlayerSequence(2, 0, spellId, definition.NativeCommandSequences);
            OnNativePlayerCommandSubmitted(actor, 2);
            BattleTargetSelection selection = (flags & 2) != 0 ? new BattleTargetSelection.RandomOnSide(BattleSide.Enemies)
                : (flags & 0x30) == 0x10 ? new BattleTargetSelection.Explicit([TargetActorReference(target)])
                : new BattleTargetSelection.SideGroup(BattleTargetSide.Enemies);
            BattleTargetRequest request = new(selection, BattleTargetEligibility.AiAvailable, false, BattleTargetReplacement.None);
            actions.Enqueue(new(actor.Id, target.Id, request, BattleAbility.EnemyAbility, spellId)
            {
                NativeQueuedCommand = 2,
                NativeAction = native,
                NativeSequence = sequence,
                FreeNativeMagic = true
            });
            actor.Atb = 0;
            actor.IsBusy = true;
            nativeLimitLevels.Remove(actor.Id);
            Log("Queued", actor.Id, target.Id, 0, $"{actor.Name}: {native.Name} queued.");
        }
    }

    private static bool SupportsExpandedAngelWingSpell(BattleNativeAction? action)
    {
        return action is { Command: 2, KernelSection: 1, TargetFlags: byte flags } && (flags & 0x40) != 0 &&
            ExpandedBattleActionRules.SupportsPlayerAction(BattleAbility.Magic, action);
    }
}
