using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public string ActionName(BattleAbility ability, int id)
    {
        return ability == BattleAbility.Attack ? "Attack" : ability == BattleAbility.EnemyAbility ? $"Enemy ability {id}" : ability == BattleAbility.Draw ? $"Draw {(id >= 64 ? GameNames.DefaultName(GameNames.GuardianForceSubject(id - 64)) : FindAction(BattleAbility.Magic, id).Name)}" : FindAction(ability, id).Name;
    }

    public void QueueAction(string actorId, string targetId, BattleAbility ability, int actionId, bool drawCast = false, IReadOnlyList<string>? magicTargets = null, string? drawCastTargetId = null)
    {
        if (ability is not (BattleAbility.Attack or BattleAbility.Magic or BattleAbility.Item or BattleAbility.Draw or BattleAbility.GuardianForce or BattleAbility.NativeCommand or BattleAbility.LimitBreak))
        {
            throw new NotSupportedException("This command requires a battle process outside the expanded host.");
        }
        if (drawCast && ability != BattleAbility.Draw)
        {
            throw new InvalidOperationException("Only Draw can request a Cast selection.");
        }
        RequireNoPendingSlotsDeal();
        RequireNativeEntryReady();
        RequireNormalGuardianFormCommands(actorId);
        if (ExpandedTimedLimitOwnsInput)
        {
            throw new InvalidOperationException("The active timed Limit owns player command input.");
        }
        if (magicTargets is not null && ability != BattleAbility.Magic)
        {
            throw new InvalidOperationException("Only a native Magic command can retain several cast targets.");
        }
        if (drawCastTargetId is not null && ability != BattleAbility.Draw)
        {
            throw new InvalidOperationException("Only Draw can retain a separate spell recipient.");
        }
        if (ability == BattleAbility.Draw)
        {
            QueuePlayerDraw(actorId, targetId, actionId, drawCast, drawCastTargetId);
            return;
        }
        if (ability is BattleAbility.NativeCommand or BattleAbility.LimitBreak)
        {
            QueueNativePlayerCommand(actorId, targetId, checked((byte)(actionId >> 16)), unchecked((ushort)actionId));
            return;
        }
        if (ability == BattleAbility.Attack)
        {
            QueueAttack(actorId, targetId);
            return;
        }
        BattleCombatant actor = GetActor(actorId);
        BattleCombatant target = GetActor(targetId);
        BattleActionDefinition action = FindAction(ability == BattleAbility.Draw ? BattleAbility.Magic : ability, actionId);
        ImmutableArray<BattleMagicCastInvocation> casts = PreparePlayerMagicTargets(actor, action, targetId, magicTargets);
        if (action.NativeAction is { } native)
        {
            ValidateNativeActionContract(native);
            ValidateIndependentGuardianItem(actor, native);
        }
        if (Outcome != BattleOutcome.Active || HasPendingExpandedEscape || !actor.IsReady || RequiresNativeCompelledCommand(actor) || !actor.Abilities.Contains(ability) || !IsValidSelectedActionTarget(actor, action, target) || HasStatus(actor, BattleStatusKind.Sleep) || ability is BattleAbility.Magic or BattleAbility.Draw && HasStatus(actor, BattleStatusKind.Silence))
        {
            throw new InvalidOperationException("Action requires an available command, a ready actor and a valid target.");
        }
            RequireExpandedAction(action);
        BattleTargetRequest request = casts.IsDefaultOrEmpty ? BuildActionTargetRequest(actor, target, action) : casts[0].Targets;
        int? reservedItemSlot = null;
        switch (ability)
        {
            case BattleAbility.Magic:
                if (GetAvailableMagicCasts(actorId, actionId) == 0)
                {
                    throw new InvalidOperationException("The selected battle magic stock is exhausted.");
                }
                break;
            case BattleAbility.Item:
                reservedItemSlot = ConsumeNativeItem(actionId);
                break;
            case BattleAbility.GuardianForce:
                if (!CanSelectExpandedGuardianForce(actor.Id, actionId))
                {
                    throw new InvalidOperationException("Summoning requires an available living junctioned GF and a caster able to charge.");
                }
                if ((!actor.EquipmentIds.Contains(actionId) || !action.Available) || guardianForms?.Values.Any(form => form.GuardianForceId == actionId) == true)
                {
                    throw new InvalidOperationException("Summoning requires a living junctioned GF.");
                }
                break;
            default:
                throw new NotSupportedException($"Battle command {ability} is unsupported.");
        }
        int magicMode = actor.IsPlayer && casts.Length == 2 && (NativeBattleStatusBits(actor) & 0x60000) == 0x40000 ? 3 : 0;
        BattleAttackCommand queued = new(actorId, targetId, request, ability, actionId, drawCast) { MagicCasts = casts, NativeMagicCastMode = magicMode, ReservedNativeItemSlot = reservedItemSlot, NativeQueuedCommand = ability switch { BattleAbility.Magic => 2, BattleAbility.Item => 4, BattleAbility.Draw => 6, BattleAbility.GuardianForce => 254, _ => null } };
        queued = EnsureMagicReservation(actor, queued with { SmartAttack = pendingSmartAttack });
        OnNativePlayerCommandSubmitted(actor, ability == BattleAbility.GuardianForce ? (byte)3 : queued.NativeQueuedCommand!.Value);
        if (ability == BattleAbility.GuardianForce)
        {
            QueueGuardianForce(actor, queued);
        }
        else
        {
            actions.Enqueue(queued);
        }
        actor.Atb = 0;
        actor.IsBusy = true;
        nativeLimitLevels.Remove(actor.Id);
        Log("Queued", actorId, targetId, 0, $"{actor.Name}: {ActionName(ability, actionId)} queued.");
    }

    private BattleTargetRequest BuildActionTargetRequest(BattleCombatant actor, BattleCombatant target, BattleActionDefinition action)
    {
        if (action.NativeAction?.TargetFlags is byte flags)
        {
            return CreatePlayerTargetRequest(actor, target, flags);
        }
        if (!action.AllTargets)
        {
            return SelectBattleTarget(target, IsNativeRevive(action), BattleTargetEligibility.Living);
        }
        BattleTargetSide side = target.IsPlayer ? BattleTargetSide.Allies : BattleTargetSide.Enemies;
        return new(new BattleTargetSelection.SideGroup(side), BattleTargetEligibility.Living, IsNativeRevive(action), BattleTargetReplacement.None);
    }

    private BattleActionDefinition FindAction(BattleAbility ability, int id)
    {
        return definition.ActionDefinitions.SingleOrDefault(action => action.Ability == ability && action.Id == id) ?? throw new NotSupportedException($"Battle {ability} {id} has no supported installed effect contract.");
    }

    private void ResolveAction(BattleCombatant actor, BattleCombatant target, BattleActionPresentation command)
    {
        if (command.NativeSlots is not null)
        {
            ResolveNativeSlotsGroup(actor, command);
            return;
        }
        if (command.Ability == BattleAbility.Draw)
        {
            ResolvePlayerDraw(actor, target, command);
            return;
        }
        if (!command.MagicCasts.IsDefaultOrEmpty)
        {
            ResolveNativeMagicGroup(actor, command);
            return;
        }
        if (command.NativeGraphic is { } graphic)
        {
            ResolveNativeGraphicCommand(actor, command, graphic);
            return;
        }
        //48FE20 commands14/238 explicitly suppress their damage/effect consumer;237 owns each accepted shot.
        if (command.NativeQueuedCommand is 14 or 238)
        {
            BattleNativeAction control = command.NativeAction ?? throw new InvalidDataException("Native Shot control lacks its original ammunition metadata.");
            ResolveInstalledNativeAction(actor, target, command, new(command.Ability, command.ActionId, control.Name ?? "Shot", BattleActionEffect.Native, 0, false) { NativeAction = control });
            Log("NativeLimitControl", actor.Id, target.Id, 0, command.NativeQueuedCommand == 14 ? "Shot started." : "Shot ended.");
            return;
        }
        if (command.IndependentGuardian is { } guardian)
        {
            ResolveIndependentGuardianItem(actor, target, command, guardian);
            return;
        }
        if (command.NativeAction is { } native)
        {
            if (IsGunbladeAction(actor, command) && native.Command is 1 or 12 or 28)
            {
                native = native with { Command = native.Command == 28 ? (byte)243 : native.Command == 12 ? (byte)248 : (byte)253, AttackType = 10, Critical = 0 };
                command = command with { NativeAction = native };
            }
            BattleActionDefinition nativeDefinition = definition.ActionDefinitions.FirstOrDefault(candidate => candidate.NativeAction?.Command == native.Command && candidate.NativeAction.ActionId == native.ActionId && candidate.NativeAction.CrisisLevel == native.CrisisLevel) ?? new(command.Ability, command.ActionId, native.Name ?? $"Native action {native.Command}/{native.ActionId}", BattleActionEffect.Native, native.Power, actor.IsPlayer == target.IsPlayer) { NativeAction = native };
            ResolveInstalledNativeAction(actor, target, command, nativeDefinition);
            return;
        }
        if (command.Ability == BattleAbility.EnemyAbility)
        {
            throw new InvalidDataException("Native action lacks its exact installed command/action metadata.");
        }
        if (command.Ability == BattleAbility.Attack)
        {
            BattleNativeAction attack = actor.NativeAttack ?? throw new NotSupportedException("Attack requires its installed native action.");
            BattleNativeAction prepared = IsGunbladeAttack(actor, command.Ability) ? attack with { Command = 253, AttackType = 10, Critical = 0 } : attack;
            BattleActionDefinition installed = new(BattleAbility.Attack, 0, attack.Name ?? "Attack", BattleActionEffect.Native, prepared.Power, false) { NativeAction = prepared };
            ResolveInstalledNativeAction(actor, target, command, installed);
            return;
        }
        BattleActionDefinition action = FindAction(command.Ability, command.ActionId);
        if (action.NativeAction is null)
        {
            throw new NotSupportedException("Action requires its installed native record.");
        }
        ResolveInstalledNativeAction(actor, target, command, action);
    }

    private void ApplySignedDamage(BattleCombatant actor, BattleCombatant target, int damage, string source)
    {
        if (damage < 0)
        {
            ApplyHealing(target, Math.Min(9999, -damage), source);
        }
        else
        {
            ApplyDamage(actor, target, damage, source);
        }
    }


    private int NextRandomByte()
    {
        random = random.Draw(out byte value);
        return value;
    }
    private static bool HasStatus(BattleCombatant actor, BattleStatusKind kind)
    {
        return kind switch
        {
            BattleStatusKind.Poison => (NativeSavedStatusBits(actor) & 2) != 0,
            BattleStatusKind.Blind => (NativeSavedStatusBits(actor) & 8) != 0,
            BattleStatusKind.Silence => (NativeSavedStatusBits(actor) & 16) != 0,
            BattleStatusKind.Sleep => (NativeBattleStatusBits(actor) & 1) != 0,
            BattleStatusKind.Haste => (NativeBattleStatusBits(actor) & 2) != 0,
            BattleStatusKind.Slow => (NativeBattleStatusBits(actor) & 4) != 0,
            BattleStatusKind.Regen => (NativeBattleStatusBits(actor) & 16) != 0,
            BattleStatusKind.Protect => (NativeBattleStatusBits(actor) & 32) != 0,
            BattleStatusKind.Shell => (NativeBattleStatusBits(actor) & 64) != 0,
            _ => false
        };
    }

    private static void ValidateStocks(BattleStock[] stocks, int maximum)
    {
        if (stocks is null || stocks.Length > maximum || stocks.Any(stock => stock is null || stock.Id <= 0 || stock.Count is < 1 or > 100) || stocks.Select(stock => stock.Id).Distinct().Count() != stocks.Length)
        {
            throw new InvalidDataException("Invalid battle inventory stocks.");
        }
    }
}
