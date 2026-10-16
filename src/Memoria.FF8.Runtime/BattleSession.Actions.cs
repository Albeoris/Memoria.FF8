namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public string ActionName(BattleAbility ability, int id)
    {
        return ability == BattleAbility.Attack ? "Attack" : ability == BattleAbility.EnemyAbility ? $"Enemy ability {id}" : ability == BattleAbility.Draw ? $"Draw {(id >= 64 ? GameNames.DefaultName(GameNames.GuardianForceSubject(id - 64)) : FindAction(BattleAbility.Magic, id).Name)}" : FindAction(ability, id).Name;
    }

    public void QueueAction(string actorId, string targetId, BattleAbility ability, int actionId, bool drawCast = false, IReadOnlyList<string>? magicTargets = null, string? drawCastTargetId = null)
    {
        RequireNativeEntryReady();
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
        System.Collections.Immutable.ImmutableArray<ushort> magicMasks = PrepareNativeMagicTargets(actor, action, targetId, magicTargets);
        bool revive = IsNativeRevive(action);
        if (action.NativeAction is { } native)
        {
            ValidateNativeActionContract(native);
            ValidateIndependentGuardianItem(actor, native);
        }
        bool allies = ability != BattleAbility.Draw && action.Allies;
        if (Outcome != BattleOutcome.Active || !actor.IsReady || !actor.Abilities.Contains(ability) || target.Untargetable || !target.IsActive || (actor.IsPlayer == target.IsPlayer) != allies || (!target.IsAlive && !revive) || target.IsAlive && revive || HasStatus(actor, BattleStatusKind.Sleep) || ability is BattleAbility.Magic or BattleAbility.Draw && HasStatus(actor, BattleStatusKind.Silence))
        {
            throw new InvalidOperationException("Action requires an available command, a ready actor and a valid target.");
        }
        int? reservedItemSlot = null;
        switch (ability)
        {
            case BattleAbility.Magic:
                if (!actor.MagicStocks.Any(stock => stock.Id == actionId && stock.Count > 0))
                {
                    throw new InvalidOperationException("The selected battle magic stock is exhausted.");
                }
                break;
            case BattleAbility.Item:
                reservedItemSlot = ConsumeNativeItem(actionId);
                break;
            case BattleAbility.GuardianForce:
                if ((!actor.EquipmentIds.Contains(actionId) || !action.Available))
                {
                    throw new InvalidOperationException("Summoning requires a living junctioned GF.");
                }
                break;
            default:
                throw new NotSupportedException($"Battle command {ability} is unsupported.");
        }
        BattleAttackCommand queued = new(actorId, targetId, ability, actionId, drawCast) { NativeMagicTargets = magicMasks, ReservedNativeItemSlot = reservedItemSlot, NativeQueuedCommand = ability switch { BattleAbility.Magic => 2, BattleAbility.Item => 4, BattleAbility.Draw => 6, BattleAbility.GuardianForce => 254, _ => null } };
        OnNativePlayerCommandSubmitted(actor, ability == BattleAbility.GuardianForce ? (byte)3 : queued.NativeQueuedCommand!.Value);
        if (ability == BattleAbility.GuardianForce && action.NativeAction is not null)
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

    private BattleActionDefinition FindAction(BattleAbility ability, int id)
    {
        return definition.ActionDefinitions.SingleOrDefault(action => action.Ability == ability && action.Id == id) ?? throw new NotSupportedException($"Battle {ability} {id} has no supported installed effect contract.");
    }

    private void ResolveAction(BattleCombatant actor, BattleCombatant target, BattleActionPresentation command)
    {
        if (command.Ability == BattleAbility.Draw)
        {
            ResolvePlayerDraw(actor, target, command);
            return;
        }
        if (!command.NativeMagicTargets.IsDefaultOrEmpty)
        {
            ResolveNativeMagicGroup(actor, command);
            return;
        }
        if (command.NativeGraphic is { } graphic)
        {
            //TODO(TD-009):252 has no4922B0 formula, but its common494410 tail still needs native lane/element scratch owners for reached target receipts
            Log("NativeGraphic", actor.Id, target.Id, graphic.MagicId, $"Native graphical effect {graphic.MagicId} completed without HP/status effects.");
            return;
        }
        //48FE20 commands14/238 explicitly suppress their damage/effect consumer;237 owns each accepted shot.
        if (command.NativeQueuedCommand is 14 or 238)
        {
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
            if (actor.NativeAttack is { } attack)
            {
                BattleNativeAction nativeAttack = IsGunbladeAttack(actor, command.Ability) ? attack with { Command = 253, AttackType = 10, Critical = 0 } : attack;
                BattleActionDefinition installedAttack = new(BattleAbility.Attack, 0, attack.Name ?? "Attack", BattleActionEffect.Native, nativeAttack.Power, false) { NativeAction = nativeAttack };
                // The original deferred gunblade callback owns type10; its current contact projection is recorded underTD-009.
                ResolveInstalledNativeAction(actor, target, command, installedAttack);
                return;
            }
            // Original492C40/48F600 physical integer damage and mitigation.
            bool gunblade = IsGunbladeAttack(actor, command.Ability);
            int damage = gunblade ? NativeGunbladeAttack.Damage(actor.Attack, target.Defense, actor.PhysicalPower, NextRandomByte(), ResolveGunbladeTiming(actor, target, command)) : NativeBattleDamage.Physical(actor.Attack, target.Defense, actor.PhysicalPower, NextRandomByte());
            if (actor.ElementAttack != 0)
            {
                int element = System.Numerics.BitOperations.TrailingZeroCount((uint)actor.ElementAttack);
                damage += damage * actor.ElementAttackPercent * (800 - target.ElementDefenses[element]) / 10000;
            }
            if (HasStatus(target, BattleStatusKind.Protect))
            {
                damage = Math.Max(1, damage / 2);
            }
            if (!gunblade && HasStatus(actor, BattleStatusKind.Blind) && NextRandomByte() < 128)
            {
                Log("Miss", actor.Id, target.Id, 0, $"{actor.Name}: Attack missed.");
                return;
            }
            int beforeHp = target.CurrentHp;
            ApplySignedDamage(actor, target, damage, "Attack");
            if (actor.DrainPercent > 0 && target.CurrentHp < beforeHp)
            {
                ApplyHealing(actor, (beforeHp - target.CurrentHp) * actor.DrainPercent / 100, "Drain");
            }
            target.Effects.RemoveAll(effect => effect.Definition.Kind == BattleStatusKind.Sleep);
            return;
        }
        BattleActionDefinition action = FindAction(command.Ability == BattleAbility.Draw ? BattleAbility.Magic : command.Ability, command.ActionId);
        if (action.NativeAction is not null)
        {
            ResolveInstalledNativeAction(actor, target, command, action);
            return;
        }
        IEnumerable<BattleCombatant> targets = action.AllTargets ? combatants.Where(candidate => candidate.IsActive && !candidate.Untargetable && candidate.IsPlayer == target.IsPlayer && (action.Effect == BattleActionEffect.Revive ? !candidate.IsAlive : candidate.IsAlive)).ToArray() : [target];
        foreach (BattleCombatant recipient in targets)
        {
            switch (action.Effect)
            {
                case BattleActionEffect.Damage:
                    // Original491AD0/48FE20 magic and GF damage over current native statistic inputs.
                    int damage = command.Ability == BattleAbility.GuardianForce ? NativeBattleDamage.GuardianForce(recipient.Spirit, action.Power, action.Level, action.LevelPowerCoefficient, action.ConstantPower, action.DamagePercent, NextRandomByte()) : NativeBattleDamage.Magic(actor.Magic, recipient.Spirit, action.Power, NextRandomByte(), !actor.IsPlayer);
                    if (HasStatus(recipient, BattleStatusKind.Shell))
                    {
                        damage >>= 1;
                    }
                    if (action.Element != 0)
                    {
                        int element = System.Numerics.BitOperations.TrailingZeroCount((uint)action.Element);
                        damage = damage * (900 - recipient.ElementDefenses[element]) / 100;
                    }
                    if (command.DrawCast)
                    {
                        damage = damage * (NextRandomByte() + 10) / 150;
                    }
                    ApplySignedDamage(actor, recipient, damage, action.Name);
                    break;
                case BattleActionEffect.Heal:
                    int healing = command.Ability == BattleAbility.Item ? action.Power * 50 : NativeBattleDamage.Cure(actor.Magic, action.Power, NextRandomByte());
                    if (command.DrawCast)
                    {
                        healing = healing * (NextRandomByte() + 10) / 150;
                    }
                    ApplyHealing(recipient, Math.Min(9999, healing), action.Name);
                    break;
                case BattleActionEffect.FullHeal:
                    ApplyHealing(recipient, recipient.MaximumHp * action.Power / 16, action.Name);
                    break;
                case BattleActionEffect.Revive:
                    recipient.NativeStatus0 &= 0xFFFE;
                    recipient.CurrentHp = action.Power == 0 ? Math.Max(1, recipient.MaximumHp / 8) : recipient.MaximumHp;
                    Log("Healing", actor.Id, recipient.Id, recipient.CurrentHp, $"{action.Name}: {recipient.Name} revived.");
                    break;
                case BattleActionEffect.Scan:
                    Log("Scan", actor.Id, recipient.Id, 0, $"{recipient.Name} Lv {recipient.Level} HP {recipient.CurrentHp}/{recipient.MaximumHp}.");
                    break;
            }
            foreach (BattleStatusKind status in action.Statuses ?? [])
            {
                ApplyProjectedStatus(actor, recipient, status, action.Name, action.Effect);
            }
        }
    }

    private void ApplyProjectedStatus(BattleCombatant actor, BattleCombatant recipient, BattleStatusKind status, string name, BattleActionEffect effectKind)
    {
        recipient.Effects.RemoveAll(effect => effect.Definition.Kind == status);
        if (effectKind is BattleActionEffect.Status or BattleActionEffect.Damage)
        {
            // TODO(TD-009): Duration and periodic magnitude are bounded scaffold status tuning
            BattleStatusEffectDefinition effect = new() { Kind = status, DurationFrames = 300, TickIntervalFrames = 30, Magnitude = Math.Max(1, recipient.MaximumHp / 20), Permanent = status != BattleStatusKind.Sleep };
            recipient.Effects.Add(new BattleStatusState(effect, effect.DurationFrames, effect.TickIntervalFrames));
        }
        Log("Status", actor.Id, recipient.Id, 0, $"{name}: {status}.");
    }

    private void ApplySignedDamage(BattleCombatant actor, BattleCombatant target, int damage, string source)
    {
        if (damage < 0)
        {
            ApplyHealing(target, Math.Min(9999, -damage), source);
        }
        else
        {
            ApplyDamage(actor, target, Math.Min(9999, damage), source);
        }
    }
    private bool IsRevive(BattleAbility ability, int id)
    {
        return ability is BattleAbility.Magic or BattleAbility.Item && IsNativeRevive(FindAction(ability, id));
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
