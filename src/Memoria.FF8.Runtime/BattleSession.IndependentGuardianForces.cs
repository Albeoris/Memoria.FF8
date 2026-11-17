using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleIndependentGuardianInvocation(BattleNativeAction? Action, byte Level, int? FailureMessage);

public sealed partial class BattleSession
{
    private BattleNativeAction? IndependentGuardianItem(BattleAttackCommand command)
    {
        return command.NativeAction ?? (command.Ability == BattleAbility.Item ? FindAction(BattleAbility.Item, command.ActionId).NativeAction : null);
    }

    private bool SupportsExpandedIndependentGuardianItem(BattleNativeAction item)
    {
        if (item is not { Command: 4, KernelSection: 7, AttackType: 14, HitCount: 1, TargetFlags: byte flags } || (flags & 0x30) is not (0 or 0x10))
        {
            return false;
        }
        if (item.ActionId == 30)
        {
            return Enumerable.Range(0, 4).All(variant => HasChild(variant + 2, 11));
        }
        return HasChild(item.MagicId, item.ActionId == 31 ? (byte)11 : (byte)28);

        bool HasChild(int id, byte attackType)
        {
            return definition.NativeActions.Count(action => action is { Command: 244, KernelSection: 9, HitCount: 1, Statuses0: 0, Statuses1: 0 } &&
                action.ActionId == id && action.AttackType == attackType && ExpandedBattleActionRules.SupportsContacts(action)) == 1;
        }
    }

    private bool HasAcquiredGuardianForce => guardianForces.Values.Any(gf => gf.Acquired);

    private byte IndependentGuardianLevel()
    {
        BattleGuardianForceVital[] acquired = guardianForces.Values.Where(gf => gf.Acquired).ToArray();
        if (acquired.Length == 0)
        {
            throw new NotSupportedException("Independent GF invocation requires the original acquired-GF level average.");
        }
        return checked((byte)(acquired.Sum(gf => FindAction(BattleAbility.GuardianForce, gf.Id).Level) / acquired.Length));
    }

    private void ValidateIndependentGuardianItem(BattleCombatant actor, BattleNativeAction item)
    {
        if (!actor.IsPlayer || item.AttackType != 14)
        {
            return;
        }
        if (item.ActionId == 30)
        {
            NativePocketStationState pocket = definition.NativePocketStation ?? throw new NotSupportedException("Gysahl Greens requires its canonical PocketStation saved block.");
            pocket.Validate();
            if ((pocket.Flags & 3) == 3 && (pocket.ChocoboName is null || definition.NativeChocoboName is null))
            {
                throw new NotSupportedException("Away Chocobo message requires its original saved12-byte name and decoded text projection.");
            }
            if ((pocket.Flags & 3) == 1 && pocket.AttackVariant > 3)
            {
                throw new NotSupportedException("The expanded host requires a supported PocketStation attack variant.");
            }
        }
        else
        {
            _ = IndependentGuardianLevel();
            _ = FindInstalledNativeAction(244, item.MagicId);
            if (item.ActionId == 31)
            {
                _ = RequireCompleteNativeSavedBattleFlags();
                if (definition.NativeFieldBattleRules is null)
                {
                    throw new NotSupportedException("Phoenix Pinion requires the current field battle prohibition byte.");
                }
            }
        }
    }

    private BattleAttackCommand PrepareIndependentGuardianItem(BattleCombatant actor, BattleAttackCommand command)
    {
        BattleNativeAction? item = IndependentGuardianItem(command);
        if (item?.Command is 240 or 245)
        {
            return command with { IndependentGuardian = new(item, IndependentGuardianLevel(), null) };
        }
        if (!actor.IsPlayer || item?.AttackType != 14)
        {
            return command;
        }
        BattleIndependentGuardianInvocation invocation = PrepareIndependentGuardianInvocation(actor, item);
            return command with { IndependentGuardian = invocation };
    }

    private BattleIndependentGuardianInvocation PrepareIndependentGuardianInvocation(BattleCombatant actor, BattleNativeAction item)
    {
        ValidateIndependentGuardianItem(actor, item);
        BattleNativeAction? effect;
        byte level;
        int? failure = null;
        if (item.ActionId == 30)
        {
            NativePocketStationState pocket = definition.NativePocketStation!;
            level = pocket.Level == 0 ? (byte)1 : pocket.Level;
            if ((pocket.Flags & 1) == 0)
            {
                effect = null;
                failure = 55;
            }
            else if ((pocket.Flags & 2) != 0)
            {
                effect = null;
                failure = 56;
            }
            else
            {
                effect = FindInstalledNativeAction(244, checked((ushort)(pocket.AttackVariant + 2)));
            }
        }
        else
        {
            effect = FindInstalledNativeAction(244, item.MagicId);
            level = IndependentGuardianLevel();
        }
        return new(effect, level, failure);
    }

    private void PublishIndependentGuardianFailure(BattleCombatant actor, BattleCombatant target, int message)
    {
        string text = NativeBattleGlobalText(message);
        if (message == 56)
        {
            //48D200 ->495210 prefixes47E4A0's saved name and the first encoded character of global11.
            string separator = NativeBattleGlobalText(11);
            text = definition.NativeChocoboName + separator[..1] + text;
        }
        QueueNativeSpecialText(actor, target, text);
    }

    private (BattleActionPresentation Command, ImmutableArray<ExpandedBattleContact> Contacts, NativeBattleActionHeaderPreparation Header) ResolveExpandedPhoenixRevival(BattleCombatant actor, BattleActionPresentation source)
    {
        // 484050 selects every allocated party row with the KO modifier before command 0/8 runs.
        BattleNativeAction revival = NativePhoenixRevivalAction() with { Name = source.NativeAction!.Name };
        BattleActionDefinition action = new(BattleAbility.Item, source.ActionId, revival.Name ?? "Rebirth Flame", BattleActionEffect.Native, 0, true) { NativeAction = revival };
        BattleTargetRequest request = new(new BattleTargetSelection.SideGroup(BattleTargetSide.Allies), BattleTargetEligibility.SavedStatus, true, BattleTargetReplacement.UseCurrentBattlePolicy);
        BattleActionPresentation command = source with { NativeQueuedCommand = 0, NativeAction = revival, TargetRequest = request };
        NativeBattleActionHeaderPreparation header = BuildExpandedActionHeaderPreparation(command, revival);
        ReserveExpandedAiInvocation(command, header);
        NativeBattleActionScratch previousScratch = nativeActionScratch;
        bool previousDeferral = deferringExpandedImpacts;
        resolvingNativeAction = command;
        nativeActionScratch = nativeActionScratch with { Command = 0, Action = revival.ActionId };
        try
        {
            deferringExpandedImpacts = true;
            var plan = PlanNativeHits(actor, command, revival);
            NativeContactPlanResult produced = ExecuteHostedContactPlan(actor, command, action, revival, plan);
            return (command, ToHostedContacts(produced), header);
        }
        finally
        {
            resolvingNativeAction = source;
            deferringExpandedImpacts = previousDeferral;
            nativeActionScratch = nativeActionScratch with { Command = previousScratch.Command, Action = previousScratch.Action };
        }
    }

    private void ValidateIndependentGuardianInvocation(BattleCombatant actor, BattleActionPresentation command)
    {
        BattleNativeAction? item = command.NativeAction ?? (command.Ability == BattleAbility.Item ? FindAction(BattleAbility.Item, command.ActionId).NativeAction : null);
        if (item?.Command is 240 or 245)
        {
            BattleIndependentGuardianInvocation support = command.IndependentGuardian ?? throw new InvalidDataException("Native support action lacks its prepared acquired-GF level owner.");
            if (support.Level != IndependentGuardianLevel() || support.FailureMessage is not null || !System.Text.Json.JsonElement.DeepEquals(System.Text.Json.JsonSerializer.SerializeToElement(support.Action), System.Text.Json.JsonSerializer.SerializeToElement(item)))
            {
                throw new InvalidDataException("Saved native support invocation differs from its original action and acquired-GF level.");
            }
            return;
        }
        if (!actor.IsPlayer || item?.AttackType != 14)
        {
            if (command.IndependentGuardian is not null)
            {
                throw new InvalidDataException("Independent GF invocation belongs to a different item command.");
            }
            return;
        }
        BattleIndependentGuardianInvocation invocation = command.IndependentGuardian ?? throw new InvalidDataException("Active GF item lacks its prepared original invocation.");
        if ((!SupportsExpandedIndependentGuardianItem(item)))
        {
            throw new InvalidDataException("Saved expanded guardian item differs from its logical target or supported child contract.");
        }
        byte level;
        int? failure = null;
        ushort actionId = item.MagicId;
        if (item.ActionId == 30)
        {
            NativePocketStationState pocket = definition.NativePocketStation ?? throw new InvalidDataException("Saved GF item lacks its PocketStation owner.");
            level = pocket.Level == 0 ? (byte)1 : pocket.Level;
            failure = (pocket.Flags & 1) == 0 ? 55 : (pocket.Flags & 2) != 0 ? 56 : null;
            if (failure is null && pocket.AttackVariant > 3)
            {
                throw new InvalidDataException("Saved expanded guardian item requires a supported PocketStation attack variant.");
            }
            actionId = checked((ushort)(pocket.AttackVariant + 2));
        }
        else
        {
            level = IndependentGuardianLevel();
        }
        bool sameAction = invocation.Action is null ? failure is not null : failure is null && System.Text.Json.JsonElement.DeepEquals(System.Text.Json.JsonSerializer.SerializeToElement(invocation.Action), System.Text.Json.JsonSerializer.SerializeToElement(FindInstalledNativeAction(244, actionId)));
        if (invocation.Level != level || invocation.FailureMessage != failure || !sameAction)
        {
            throw new InvalidDataException("Saved independent GF invocation differs from its original item and saved owners.");
        }
    }
}
