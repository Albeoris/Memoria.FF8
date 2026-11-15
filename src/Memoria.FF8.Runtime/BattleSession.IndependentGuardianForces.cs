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
            if (UsesExpandedRoster && (pocket.Flags & 3) == 1 && pocket.AttackVariant > 3)
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
                if (UsesExpandedRoster && definition.NativeFieldBattleRules is null)
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
        if (UsesExpandedRoster)
        {
            return command with { IndependentGuardian = invocation };
        }
        BattleTargetRequest request = command.TargetRequest.Selection is not BattleTargetSelection.None
            ? command.TargetRequest
            : CreatePlayerTargetRequest(actor, byId[command.TargetId], item.TargetFlags ?? throw new InvalidDataException("GF summon item lacks its original target flags."));
        return command with { IndependentGuardian = invocation, TargetRequest = request };
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
            if (item.ActionId == 31 && !UsesExpandedRoster)
            {
                SetNativeSavedBattleFlag(4);
            }
        }
        return new(effect, level, failure);
    }

    private void ResolveIndependentGuardianItem(BattleCombatant actor, BattleCombatant target, BattleActionPresentation command, BattleIndependentGuardianInvocation invocation)
    {
        if (invocation.FailureMessage is int message)
        {
            BattleNativeAction item = command.NativeAction ?? FindAction(BattleAbility.Item, command.ActionId).NativeAction ?? throw new InvalidDataException("Independent GF failure lacks its original item record.");
            //48D200 normalizes244/FFF8 and244/FFF9 to0/9;48E830 still writes its hidden zero contacts before DF4 suppresses the published count.
            BattleNativeAction control = item with { Command = 0, ActionId = 9, KernelSection = -1, KernelIndex = -1, MagicId = 0, AttackType = 0, Power = 0, HitCount = 1, AttackFlags = 3, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null };
            BattleActionPresentation failed = command with { NativeQueuedCommand = 0, NativeAction = control };
            BattleActionDefinition failedAction = new(BattleAbility.Item, command.ActionId, item.Name ?? "GF item", BattleActionEffect.Native, 0, false) { NativeAction = control };
            ResolveInstalledNativeAction(actor, target, failed, failedAction);
            PublishIndependentGuardianFailure(actor, target, message);
            return;
        }
        BattleNativeAction native = invocation.Action ?? throw new InvalidDataException("Independent GF invocation lacks its original action record.");
        BattleActionDefinition action = new(BattleAbility.Item, command.ActionId, native.Name ?? "GF", BattleActionEffect.Native, native.Power, actor.IsPlayer == target.IsPlayer, Level: invocation.Level) { NativeAction = native };
        //485610 retains queued E10=4/13 while48D200 normalizes the working/header command to244.
        BattleActionPresentation resolved = command with { NativeAction = native };
        ResolveInstalledNativeAction(actor, target, resolved, action);
        if ((command.NativeAction?.ActionId ?? command.ActionId) == 31)
        {
            CompletePhoenixRevival(actor, resolved);
        }
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

    private void CompletePhoenixRevival(BattleCombatant actor, BattleActionPresentation source)
    {
        //485460 ->484050 follows Phoenix's damage with command0/8 and the nativeC007 party mask.
        BattleNativeAction revival = NativePhoenixRevivalAction();
        BattleActionDefinition action = new(BattleAbility.EnemyAbility, 8, "Rebirth Flame", BattleActionEffect.Native, 0, true) { NativeAction = revival };
        BattleActionPresentation command = source with { NativeQueuedCommand = 0, NativeAction = revival, TargetRequest = new(new BattleTargetSelection.SideGroup(BattleTargetSide.Allies), BattleTargetEligibility.SavedStatus, true, BattleTargetReplacement.UseCurrentBattlePolicy) };
        NativeBattleActionProductionContext previous = nativeProductionContext ?? throw new InvalidDataException("Native484050 requires its completed485610 admission context.");
        //485610 LABEL104 resets DF0's repeat BYTE before485460 calls484050; this direct0/8 is not a second Double/Triple cast.
        nativeProductionContext = previous with { RepeatMode = 0 };
        try
        {
            ResolveInstalledNativeAction(actor, actor, command, action);
        }
        finally
        {
            nativeProductionContext = previous;
        }
    }

    private (BattleActionPresentation Command, ImmutableArray<ExpandedBattleContact> Contacts) ResolveExpandedPhoenixRevival(BattleCombatant actor, BattleActionPresentation source)
    {
        // 484050 selects every allocated party row with the KO modifier before command 0/8 runs.
        BattleNativeAction revival = NativePhoenixRevivalAction() with { Name = source.NativeAction!.Name };
        BattleActionDefinition action = new(BattleAbility.Item, source.ActionId, revival.Name ?? "Rebirth Flame", BattleActionEffect.Native, 0, true) { NativeAction = revival };
        BattleActionPresentation command = source with { NativeQueuedCommand = 0, NativeAction = revival };
        BattleCombatant[] recipients = allies.Where(ally => ally.IsActive && !ally.Untargetable).ToArray();
        NativeBattleActionScratch previousScratch = nativeActionScratch;
        resolvingNativeAction = command;
        nativeActionScratch = nativeActionScratch with { Command = 0, Action = revival.ActionId };
        try
        {
            (var contacts, _) = ResolveExpandedHitPlan(actor, command, action, revival, [recipients]);
            return (command, contacts);
        }
        finally
        {
            resolvingNativeAction = source;
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
        if (UsesExpandedRoster && (!SupportsExpandedIndependentGuardianItem(item)))
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
            if (UsesExpandedRoster && failure is null && pocket.AttackVariant > 3)
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
