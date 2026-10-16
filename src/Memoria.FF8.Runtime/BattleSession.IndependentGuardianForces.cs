namespace Memoria.FF8.Runtime;

public sealed record BattleIndependentGuardianInvocation(BattleNativeAction? Action, byte Level, int? FailureMessage);

public sealed partial class BattleSession
{
    private BattleNativeAction? IndependentGuardianItem(BattleAttackCommand command)
    {
        return command.NativeAction ?? (command.Ability == BattleAbility.Item ? FindAction(BattleAbility.Item, command.ActionId).NativeAction : null);
    }

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
        }
        else
        {
            _ = IndependentGuardianLevel();
            _ = FindInstalledNativeAction(244, item.MagicId);
            if (item.ActionId == 31)
            {
                _ = RequireCompleteNativeSavedBattleFlags();
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
            if (item.ActionId == 31)
            {
                SetNativeSavedBattleFlag(4);
            }
        }
        ushort mask = command.NativeTargetMask != 0 ? command.NativeTargetMask : NativeBattleLimitRules.PlayerTargetMask(item.TargetFlags ?? throw new InvalidDataException("GF summon item lacks its original target flags."), byId[command.TargetId].NativeIndex);
        return command with { IndependentGuardian = new(effect, level, failure), NativeTargetMask = mask };
    }

    private void ResolveIndependentGuardianItem(BattleCombatant actor, BattleCombatant target, BattleActionPresentation command, BattleIndependentGuardianInvocation invocation)
    {
        if (invocation.FailureMessage is int message)
        {
            string text = NativeBattleGlobalText(message);
            if (message == 56)
            {
                //48D200 ->495210 prefixes47E4A0's saved name and the first encoded character of global11.
                string separator = NativeBattleGlobalText(11);
                text = definition.NativeChocoboName + separator[..1] + text;
            }
            QueueNativeSpecialText(actor, target, text);
            return;
        }
        BattleNativeAction native = invocation.Action ?? throw new InvalidDataException("Independent GF invocation lacks its original action record.");
        BattleActionDefinition action = new(BattleAbility.Item, command.ActionId, native.Name ?? "GF", BattleActionEffect.Native, native.Power, actor.IsPlayer == target.IsPlayer, Level: invocation.Level) { NativeAction = native };
        BattleActionPresentation resolved = command with { NativeAction = native };
        ResolveInstalledNativeAction(actor, target, resolved, action);
        if ((command.NativeAction?.ActionId ?? command.ActionId) == 31)
        {
            CompletePhoenixRevival(actor, resolved, native);
        }
    }

    private void CompletePhoenixRevival(BattleCombatant actor, BattleActionPresentation source, BattleNativeAction phoenix)
    {
        //485460 ->484050 follows Phoenix's damage with command0/8 and the nativeC007 party mask.
        BattleNativeAction revival = NativePhoenixRevivalAction();
        BattleActionDefinition action = new(BattleAbility.EnemyAbility, 8, "Rebirth Flame", BattleActionEffect.Native, 0, true) { NativeAction = revival };
        BattleActionPresentation command = source with { NativeQueuedCommand = 0, NativeAction = revival, NativeTargetMask = 0xC007 };
        ResolveInstalledNativeAction(actor, actor, command, action);
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
        byte level;
        int? failure = null;
        ushort actionId = item.MagicId;
        if (item.ActionId == 30)
        {
            NativePocketStationState pocket = definition.NativePocketStation ?? throw new InvalidDataException("Saved GF item lacks its PocketStation owner.");
            level = pocket.Level == 0 ? (byte)1 : pocket.Level;
            failure = (pocket.Flags & 1) == 0 ? 55 : (pocket.Flags & 2) != 0 ? 56 : null;
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
