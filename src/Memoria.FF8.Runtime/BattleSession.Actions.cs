namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public string ActionName(BattleAbility ability, int id)
    {
        return ability == BattleAbility.Attack ? "Attack" : ability == BattleAbility.EnemyAbility ? $"Enemy ability {id}" : ability == BattleAbility.Draw ? $"Draw {FindAction(BattleAbility.Magic, id).Name}" : FindAction(ability, id).Name;
    }

    public void QueueAction(string actorId, string targetId, BattleAbility ability, int actionId, bool drawCast = false)
    {
        if (ability == BattleAbility.Attack)
        {
            QueueAttack(actorId, targetId);
            return;
        }
        BattleCombatant actor = GetActor(actorId);
        BattleCombatant target = GetActor(targetId);
        BattleActionDefinition action = FindAction(ability == BattleAbility.Draw ? BattleAbility.Magic : ability, actionId);
        bool revive = action.Effect == BattleActionEffect.Revive;
        bool allies = ability != BattleAbility.Draw && action.Allies;
        if (Outcome != BattleOutcome.Active || !actor.IsReady || !actor.Abilities.Contains(ability) || target.Untargetable || !target.IsActive || (actor.IsPlayer == target.IsPlayer) != allies || (!target.IsAlive && !revive) || target.IsAlive && revive || HasStatus(actor, BattleStatusKind.Sleep) || ability is BattleAbility.Magic or BattleAbility.Draw && HasStatus(actor, BattleStatusKind.Silence))
        {
            throw new InvalidOperationException("Action requires an available command, a ready actor and a valid target.");
        }
        switch (ability)
        {
            case BattleAbility.Magic:
                Consume(actor.Stocks, actionId);
                break;
            case BattleAbility.Item:
                Consume(items, actionId);
                break;
            case BattleAbility.Draw:
                if (!target.DrawSpells.Any(spell => spell.Id == actionId) || (!drawCast && !actor.Stocks.Any(stock => stock.Id == actionId) && actor.Stocks.Count >= 32) || !drawCast && actor.Stocks.Any(stock => stock.Id == actionId && stock.Count >= 100))
                {
                    throw new InvalidOperationException("Draw requires an installed enemy spell and available magic capacity.");
                }
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
        actor.Atb = 0;
        actor.IsBusy = true;
        actions.Enqueue(new BattleAttackCommand(actorId, targetId, ability, actionId, drawCast));
        Log("Queued", actorId, targetId, 0, $"{actor.Name}: {ActionName(ability, actionId)} queued.");
    }

    private BattleActionDefinition FindAction(BattleAbility ability, int id)
    {
        return definition.ActionDefinitions.SingleOrDefault(action => action.Ability == ability && action.Id == id) ?? throw new NotSupportedException($"Battle {ability} {id} has no supported installed effect contract.");
    }

    private void ResolveAction(BattleCombatant actor, BattleCombatant target, BattleActionPresentation command)
    {
        if (command.Ability == BattleAbility.EnemyAbility)
        {
            ResolveNativeAbility(actor, target, command);
            return;
        }
        if (command.Ability == BattleAbility.Attack)
        {
            // Original492C40/48F600 physical integer damage and mitigation.
            int damage = NativeBattleDamage.Physical(actor.Attack, target.Defense, actor.PhysicalPower, NextRandomByte());
            if (actor.ElementAttack != 0)
            {
                int element = System.Numerics.BitOperations.TrailingZeroCount((uint)actor.ElementAttack);
                damage += damage * actor.ElementAttackPercent * (800 - target.ElementDefenses[element]) / 10000;
            }
            if (HasStatus(target, BattleStatusKind.Protect))
            {
                damage = Math.Max(1, damage / 2);
            }
            if (HasStatus(actor, BattleStatusKind.Blind) && NextRandomByte() < 128)
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
        if (command.Ability == BattleAbility.Draw)
        {
            BattleDrawSpell spell = target.DrawSpells.Single(draw => draw.Id == command.ActionId);
            // Native48FD20; the RNG/stat owners remain explicit scaffold inputs under TD-009.
            int quantity = Math.Clamp(((((actor.Level - target.Level + 10) >> 1) - action.DrawResist + (NextRandomByte() & 31) + 1 + actor.Magic) / 5) - spell.Resistance, 0, 9);
            if (!command.DrawCast)
            {
                int index = actor.Stocks.FindIndex(stock => stock.Id == command.ActionId);
                int previous = index < 0 ? 0 : actor.Stocks[index].Count;
                int added = Math.Min(100 - previous, quantity);
                if (added > 0)
                {
                    BattleStock stock = new(command.ActionId, previous + added);
                    if (index < 0)
                    {
                        actor.Stocks.Add(stock);
                    }
                    else
                    {
                        actor.Stocks[index] = stock;
                    }
                }
                Log("Draw", actor.Id, target.Id, added, $"{actor.Name} stocks {added} {action.Name}.");
                return;
            }
            if (quantity == 0)
            {
                Log("Draw", actor.Id, target.Id, 0, "Draw failed.");
                return;
            }
            target = action.Allies ? actor : target;
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
                    recipient.CurrentHp = action.Power == 0 ? Math.Max(1, recipient.MaximumHp / 8) : recipient.MaximumHp;
                    Log("Healing", actor.Id, recipient.Id, recipient.CurrentHp, $"{action.Name}: {recipient.Name} revived.");
                    break;
                case BattleActionEffect.Scan:
                    Log("Scan", actor.Id, recipient.Id, 0, $"{recipient.Name} Lv {recipient.Level} HP {recipient.CurrentHp}/{recipient.MaximumHp}.");
                    break;
            }
            foreach (BattleStatusKind status in action.Statuses ?? [])
            {
                recipient.Effects.RemoveAll(effect => effect.Definition.Kind == status);
                if (action.Effect is BattleActionEffect.Status or BattleActionEffect.Damage)
                {
                    // TODO(TD-009): Duration and periodic magnitude are bounded scaffold status tuning
                    BattleStatusEffectDefinition effect = new() { Kind = status, DurationFrames = 300, TickIntervalFrames = 30, Magnitude = Math.Max(1, recipient.MaximumHp / 20), Permanent = status != BattleStatusKind.Sleep };
                    recipient.Effects.Add(new BattleStatusState(effect, effect.DurationFrames, effect.TickIntervalFrames));
                }
                Log("Status", actor.Id, recipient.Id, 0, $"{action.Name}: {status}.");
            }
        }
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
        return ability is BattleAbility.Magic or BattleAbility.Item && FindAction(ability, id).Effect == BattleActionEffect.Revive;
    }

    private int NextRandomByte()
    {
        random = random.Draw(out byte value);
        return value;
    }
    private static bool HasStatus(BattleCombatant actor, BattleStatusKind kind)
    {
        return actor.Effects.Any(effect => effect.Definition.Kind == kind);
    }

    private static void Consume(List<BattleStock> stocks, int id)
    {
        int index = stocks.FindIndex(stock => stock.Id == id && stock.Count > 0);
        if (index < 0)
        {
            throw new InvalidOperationException("The selected battle stock is exhausted.");
        }
        if (stocks[index].Count == 1)
        {
            stocks.RemoveAt(index);
        }
        else
        {
            stocks[index] = stocks[index] with { Count = stocks[index].Count - 1 };
        }
    }

    private static void ValidateStocks(BattleStock[] stocks, int maximum)
    {
        if (stocks is null || stocks.Length > maximum || stocks.Any(stock => stock is null || stock.Id <= 0 || stock.Count is < 1 or > 100) || stocks.Select(stock => stock.Id).Distinct().Count() != stocks.Length)
        {
            throw new InvalidDataException("Invalid battle inventory stocks.");
        }
    }
}
