namespace Memoria.FF8.Runtime;

/// <summary>
/// Deterministic playable battle scaffold. The 30 Hz dispatch, gauge, damage formula,
/// AI and presentation timings are explicit product tuning, not original FF8 parity.
/// </summary>
public sealed partial class BattleSession
{
    // TODO(TD-009): Replace scaffold gauge and action timings with recovered battle contracts
    public const int FramesPerSecond = 30;
    public const int AtbMaximum = 10_000;
    public const int AttackWindupFrames = 12;
    public const int AttackRecoveryFrames = 12;
    public const int MaximumAdvanceFrames = 18_000;
    private readonly BattleDefinition definition;
    private readonly List<BattleCombatant> combatants;
    private readonly Dictionary<string, BattleCombatant> byId;
    private readonly Dictionary<BattleStatusKind, IBattleStatusEffectHandler> handlers;
    private readonly Queue<BattleAttackCommand> actions = new();
    private readonly List<BattleEvent> events = [];
    private readonly List<BattleStock> items = [];
    private NativeBattleRandomState random;

    public BattleSession(BattleDefinition definition, BattleAtbMode mode) : this(definition, mode, [])
    {
    }

    public BattleSession(BattleDefinition definition, BattleAtbMode mode, IEnumerable<IBattleStatusEffectHandler> additionalHandlers) : this(definition, mode, additionalHandlers, true)
    {
    }

    private BattleSession(BattleDefinition definition, BattleAtbMode mode, IEnumerable<IBattleStatusEffectHandler> additionalHandlers, bool initializeNativeAi)
    {
        ValidateMode(mode);
        handlers = Enum.GetValues<BattleStatusKind>().ToDictionary(kind => kind, kind => (IBattleStatusEffectHandler)new ScaffoldBattleEffectHandler(kind));
        HashSet<BattleStatusKind> customKinds = [];
        foreach (IBattleStatusEffectHandler handler in additionalHandlers)
        {
            if (!Enum.IsDefined(handler.Kind) || handler.AtbRatePercent is < 1 or > 1_000 || !customKinds.Add(handler.Kind))
            {
                throw new ArgumentException("Duplicate or invalid status effect handler.", nameof(additionalHandlers));
            }
            handlers[handler.Kind] = handler;
        }
        this.definition = CloneDefinition(definition);
        ValidateDefinition(this.definition);
        combatants = this.definition.Combatants.Select(actor => new BattleCombatant(actor)).ToList();
        Combatants = combatants.AsReadOnly();
        byId = combatants.ToDictionary(actor => actor.Id, StringComparer.Ordinal);
        items.AddRange(this.definition.Items);
        random = this.definition.InitialRandom ?? NativeBattleRandomState.Initialize(this.definition.NativeSeed);
        random.Validate();
        Mode = mode;
        if (initializeNativeAi)
        {
            InitializeNativeParticipants();
        }
        Outcome = DetermineOutcome();
    }

    public BattleAtbMode Mode { get; private set; }
    public long Frame { get; private set; }
    public BattleOutcome Outcome { get; private set; }
    public IReadOnlyList<BattleCombatant> Combatants { get; }
    public IReadOnlyList<BattleEvent> Events => events.AsReadOnly();
    public IReadOnlyList<BattleAttackCommand> QueuedActions => actions.ToArray();
    public BattleActionPresentation? ActiveAction { get; private set; }
    public string Name => definition.Name;
    public int? EncounterId => definition.EncounterId;
    public IReadOnlyList<BattleStock> Items => items.AsReadOnly();
    public IReadOnlyList<BattleActionDefinition> ActionDefinitions => definition.ActionDefinitions.Select(action => action with { Statuses = action.Statuses?.ToArray() }).ToArray();

    public void SetMode(BattleAtbMode mode)
    {
        ValidateMode(mode);
        Mode = mode;
    }

    public void Tick()
    {
        if (Outcome != BattleOutcome.Active || (Mode == BattleAtbMode.TurnBased && actions.Count == 0 && ActiveAction is null))
        {
            return;
        }
        DispatchFrame();
    }

    public int AdvanceTurn(int maxFrames = MaximumAdvanceFrames)
    {
        if (Mode != BattleAtbMode.TurnBased)
        {
            throw new InvalidOperationException("Advance Turn requires Turn-Based mode.");
        }
        if (maxFrames is < 1 or > MaximumAdvanceFrames)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFrames));
        }
        HashSet<string> waiting = combatants.Where(actor => actor.IsAlive && !actor.IsReady).Select(actor => actor.Id).ToHashSet(StringComparer.Ordinal);
        if (Outcome != BattleOutcome.Active || waiting.Count == 0)
        {
            return 0;
        }
        // The same dispatcher runs on owned state so a bounded failure cannot leave partially advanced frames.
        BattleSession candidate = Restore(Capture(), handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler));
        for (int count = 1; count <= maxFrames; count++)
        {
            HashSet<string> becameReady = candidate.DispatchFrame();
            if (candidate.Outcome != BattleOutcome.Active || becameReady.Overlaps(waiting))
            {
                PublishCandidate(candidate);
                return count;
            }
        }
        throw new TimeoutException($"No living unready combatant became ready within {maxFrames} dispatched battle frames.");
    }

    public void QueueAttack(string actorId, string targetId)
    {
        BattleCombatant actor = GetActor(actorId);
        BattleCombatant target = GetActor(targetId);
        if (Outcome != BattleOutcome.Active || !actor.IsReady || !target.IsAlive || target.Untargetable || actor.IsPlayer == target.IsPlayer || !actor.Abilities.Contains(BattleAbility.Attack) || HasStatus(actor, BattleStatusKind.Sleep))
        {
            throw new InvalidOperationException("Attack requires a ready living actor with Attack and a living opponent in an active battle.");
        }
        EnqueueAttack(actor, target);
    }

    public void InstantVictory()
    {
        if (Outcome != BattleOutcome.Active)
        {
            throw new InvalidOperationException("Debug victory requires an active battle.");
        }
        foreach (BattleCombatant enemy in combatants.Where(actor => !actor.IsPlayer))
        {
            enemy.CurrentHp = 0;
            enemy.Atb = 0;
        }
        Log("debug-victory", null, null, 0, "Debug shortcut: all enemies defeated.");
        CompleteBattleIfNeeded();
    }

    public void InstantDefeat()
    {
        if (Outcome != BattleOutcome.Active)
        {
            throw new InvalidOperationException("Debug defeat requires an active battle.");
        }
        foreach (BattleCombatant actor in combatants.Where(actor => actor.IsPlayer))
        {
            actor.CurrentHp = 0;
            actor.Atb = 0;
        }
        Log("debug-defeat", null, null, 0, "Debug shortcut: all party members defeated.");
        CompleteBattleIfNeeded();
    }

    public int AtbGainPerFrame(BattleCombatant actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!byId.TryGetValue(actor.Id, out BattleCombatant? ownActor) || !ReferenceEquals(actor, ownActor))
        {
            throw new ArgumentException("Combatant belongs to another battle.", nameof(actor));
        }
        int ratePercent = 100;
        foreach (BattleStatusState effect in actor.Effects)
        {
            ratePercent = checked(ratePercent * handlers[effect.Definition.Kind].AtbRatePercent / 100);
        }
        return CalculateAtbGain(actor.Speed, ratePercent);
    }

    // Presentation estimate holds the current status rate constant. Dispatch remains authoritative.
    public int? EstimatedReadyFrames(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        if (!actor.IsAlive || Outcome != BattleOutcome.Active)
        {
            return null;
        }
        int actionDelay = 0;
        if (actor.IsBusy)
        {
            if (ActiveAction is { } active)
            {
                actionDelay = active.RemainingFrames + (active.Phase == BattleActionPhase.Windup ? AttackRecoveryFrames : 0);
                if (active.ActorId == actorId)
                {
                    return actionDelay + (AtbMaximum + AtbGainPerFrame(actor) - 1) / AtbGainPerFrame(actor);
                }
            }
            foreach (BattleAttackCommand command in actions)
            {
                actionDelay += 1 + AttackWindupFrames + AttackRecoveryFrames;
                if (command.ActorId == actorId)
                {
                    break;
                }
            }
        }
        int gain = AtbGainPerFrame(actor);
        return actionDelay + (AtbMaximum - actor.Atb + gain - 1) / gain;
    }

    public BattleSnapshot Capture()
    {
        return new BattleSnapshot
        {
            Definition = CloneDefinition(definition),
            Mode = Mode,
            Frame = Frame,
            Outcome = Outcome,
            Combatants = combatants.Select(actor => new BattleCombatantSnapshot(actor.Id, actor.CurrentHp, actor.Atb, actor.StatusEffects.ToArray()) { MagicStocks = actor.Stocks.ToArray(), NativeParticipant = actor.SceneSlot is null && actor.NativeAi is null ? null : CaptureNativeParticipant(actor) }).ToArray(),
            QueuedActions = actions.ToArray(),
            ActiveAction = ActiveAction,
            Events = events.ToArray(),
            Items = items.ToArray(),
            EnemyDefeats = enemyDefeats.ToArray(),
            Random = random,
            NativeAiVariables = combatants.Any(actor => actor.NativeAi is not null) ? nativeAiVariables.ToArray() : null,
            CustomHandlers = handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler).Select(handler => handler.Kind).Order().ToArray()
        };
    }

    public static BattleSession Restore(BattleSnapshot snapshot, IEnumerable<IBattleStatusEffectHandler>? additionalHandlers = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        try
        {
            BattleSession session = new(snapshot.Definition, snapshot.Mode, additionalHandlers ?? [], false);
            session.RestoreValidated(snapshot);
            return session;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or NullReferenceException or KeyNotFoundException)
        {
            throw new InvalidDataException("Invalid battle snapshot.", error);
        }
    }

    private HashSet<string> DispatchFrame()
    {
        Frame = checked(Frame + 1);
        HashSet<string> becameReady = [];
        foreach (BattleCombatant actor in combatants)
        {
            if (!actor.IsAlive)
            {
                continue;
            }
            int ratePercent = 100;
            foreach (BattleStatusState effect in actor.Effects.ToArray())
            {
                IBattleStatusEffectHandler handler = handlers[effect.Definition.Kind];
                effect.UntilTickFrames--;
                bool periodicTick = effect.UntilTickFrames == 0;
                handler.Dispatch(new BattleEffectContext(actor, effect.Definition, Frame, periodicTick, amount => ApplyDamage(null, actor, amount, effect.Definition.Kind.ToString()), amount => ApplyHealing(actor, amount, effect.Definition.Kind.ToString())));
                ratePercent = checked(ratePercent * handler.AtbRatePercent / 100);
                if (periodicTick)
                {
                    effect.UntilTickFrames = effect.Definition.TickIntervalFrames;
                }
                if (!effect.Definition.Permanent)
                {
                    effect.RemainingFrames--;
                }
                if (!effect.Definition.Permanent && effect.RemainingFrames == 0)
                {
                    actor.Effects.Remove(effect);
                    Log("StatusExpired", actor.Id, actor.Id, 0, $"{actor.Name}: {effect.Definition.Kind} expired.");
                }
                if (!actor.IsAlive)
                {
                    break;
                }
            }
            if (actor.IsAlive && !actor.IsBusy && !actor.IsReady && !HasStatus(actor, BattleStatusKind.Sleep))
            {
                // Scaffold gauge growth: Speed units per dispatched frame, modified by active statuses.
                int gain = CalculateAtbGain(actor.Speed, ratePercent);
                actor.Atb = Math.Min(AtbMaximum, actor.Atb + gain);
                if (actor.IsReady)
                {
                    becameReady.Add(actor.Id);
                    Log("Ready", actor.Id, null, 0, $"{actor.Name} is ready.");
                }
            }
        }
        CancelDeadActions();
        CompleteBattleIfNeeded();
        if (Outcome != BattleOutcome.Active)
        {
            return becameReady;
        }
        foreach (BattleCombatant enemy in combatants.Where(actor => !actor.IsPlayer && actor.IsReady && !HasStatus(actor, BattleStatusKind.Sleep)))
        {
            if (enemy.NativeAi is not null)
            {
                enemy.Atb = 0;
                enemy.AiTurnCount = unchecked((byte)(enemy.AiTurnCount + 1));
                RunNativeAi(enemy, 1);
                continue;
            }
            // TODO(TD-009): First living opponent is scaffold AI until native enemy decision programs are implemented
            BattleCombatant target = combatants.First(actor => actor.IsPlayer && actor.IsAlive);
            EnqueueAttack(enemy, target);
        }
        DispatchAction();
        CancelDeadActions();
        CompleteBattleIfNeeded();
        return becameReady;
    }

    private void DispatchAction()
    {
        if (ActiveAction is null)
        {
            while (actions.TryDequeue(out BattleAttackCommand? command))
            {
                BattleCombatant actor = byId[command.ActorId];
                BattleCombatant target = byId[command.TargetId];
                if (!actor.IsAlive || !target.IsAlive && !IsRevive(command.Ability, command.ActionId))
                {
                    actor.IsBusy = false;
                    Log("Cancelled", actor.Id, target.Id, 0, "Attack cancelled because a participant was defeated.");
                    continue;
                }
                ActiveAction = new BattleActionPresentation(actor.Id, target.Id, BattleActionPhase.Windup, AttackWindupFrames, command.Ability, command.ActionId, command.DrawCast, command.NativeTargetSelector);
                Log("Windup", actor.Id, target.Id, 0, $"{actor.Name}: {ActionName(command.Ability, command.ActionId)} → {target.Name}.");
                return;
            }
            return;
        }
        BattleActionPresentation action = ActiveAction;
        int remaining = action.RemainingFrames - 1;
        if (remaining > 0)
        {
            ActiveAction = action with { RemainingFrames = remaining };
            return;
        }
        if (action.Phase == BattleActionPhase.Windup)
        {
            BattleCombatant actor = byId[action.ActorId];
            BattleCombatant target = byId[action.TargetId];
            if (actor.IsAlive && (target.IsAlive || IsRevive(action.Ability, action.ActionId)))
            {
                ResolveAction(actor, target, action);
            }
            ActiveAction = action with { Phase = BattleActionPhase.Recovery, RemainingFrames = AttackRecoveryFrames };
            return;
        }
        byId[action.ActorId].IsBusy = false;
        ActiveAction = null;
        Log("ActionComplete", action.ActorId, action.TargetId, 0, "Attack recovery completed.");
    }

    private void EnqueueAttack(BattleCombatant actor, BattleCombatant target)
    {
        actor.Atb = 0;
        actor.IsBusy = true;
        actions.Enqueue(new BattleAttackCommand(actor.Id, target.Id));
        Log("Queued", actor.Id, target.Id, 0, $"{actor.Name}: Attack queued.");
    }

    private void ApplyDamage(BattleCombatant? actor, BattleCombatant target, int amount, string source)
    {
        if (!target.IsAlive)
        {
            return;
        }
        int applied = Math.Min(target.CurrentHp, amount);
        target.CurrentHp -= applied;
        target.LastAttackerId = actor?.Id;
        target.LastAttackerAbility = ActiveAction?.Ability;
        Log("Damage", actor?.Id, target.Id, applied, $"{source}: {target.Name} takes {applied} damage.");
        if (target.NativeAi is not null)
        {
            RunNativeAi(target, 4);
        }
        if (!target.IsAlive)
        {
            ObserveEnemyDefeat(actor, target);
            if (target.NativeAi is not null && !target.AiDeathHandled)
            {
                target.AiDeathHandled = true;
                RunNativeAi(target, 3);
            }
            target.Atb = 0;
            target.IsBusy = false;
            Log("Defeated", actor?.Id, target.Id, 0, $"{target.Name} is defeated.");
        }
        else if (target.NativeAi is not null)
        {
            RunNativeAi(target, 2);
        }
    }

    private void ApplyHealing(BattleCombatant actor, int amount, string source)
    {
        if (!actor.IsAlive)
        {
            return;
        }
        int applied = Math.Min(actor.MaximumHp - actor.CurrentHp, amount);
        actor.CurrentHp += applied;
        if (applied != 0)
        {
            Log("Healing", actor.Id, actor.Id, applied, $"{source}: {actor.Name} recovers {applied} HP.");
        }
    }

    private void CancelDeadActions()
    {
        if (ActiveAction is { } active && (!byId[active.ActorId].IsAlive || (active.Phase == BattleActionPhase.Windup && !byId[active.TargetId].IsAlive && !IsRevive(active.Ability, active.ActionId))))
        {
            byId[active.ActorId].IsBusy = false;
            ActiveAction = null;
            Log("Cancelled", active.ActorId, active.TargetId, 0, "Defeated actor cannot complete an action.");
        }
        BattleAttackCommand[] pending = actions.ToArray();
        actions.Clear();
        foreach (BattleAttackCommand command in pending)
        {
            if (byId[command.ActorId].IsAlive && (byId[command.TargetId].IsAlive || IsRevive(command.Ability, command.ActionId)))
            {
                actions.Enqueue(command);
            }
            else
            {
                byId[command.ActorId].IsBusy = false;
                Log("Cancelled", command.ActorId, command.TargetId, 0, "Defeated actor cannot act.");
            }
        }
    }

    private BattleOutcome DetermineOutcome()
    {
        if (!combatants.Any(actor => actor.IsPlayer && actor.IsAlive))
        {
            return BattleOutcome.Defeat;
        }
        return combatants.Any(actor => !actor.IsPlayer && actor.IsAlive && !actor.Untargetable) ? BattleOutcome.Active : BattleOutcome.Victory;
    }

    private void CompleteBattleIfNeeded()
    {
        BattleOutcome outcome = DetermineOutcome();
        if (outcome == BattleOutcome.Active)
        {
            return;
        }
        Outcome = outcome;
        actions.Clear();
        ActiveAction = null;
        foreach (BattleCombatant actor in combatants)
        {
            actor.IsBusy = false;
        }
        Log("Outcome", null, null, 0, outcome.ToString());
    }

    private BattleCombatant GetActor(string id)
    {
        if (id is null || !byId.TryGetValue(id, out BattleCombatant? actor))
        {
            throw new ArgumentException("Unknown battle combatant.", nameof(id));
        }
        return actor;
    }

    private void Log(string kind, string? actorId, string? targetId, int amount, string message)
    {
        BattleAbility ability = ActiveAction is { } active && active.ActorId == actorId ? active.Ability : BattleAbility.Attack;
        events.Add(new BattleEvent(Frame, kind, actorId, targetId, amount, message, ability));
    }

    private static void ValidateMode(BattleAtbMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private static int CalculateAtbGain(int speed, int ratePercent)
    {
        return Math.Max(1, checked(speed * ratePercent / 100));
    }

    public static BattleDefinition CloneDefinition(BattleDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.Combatants);
        return source with { Combatants = source.Combatants.Select(actor => actor with { EquipmentIds = actor.EquipmentIds.ToArray(), Abilities = actor.Abilities.ToArray(), StatusEffects = actor.StatusEffects.ToArray(), MagicStocks = actor.MagicStocks.ToArray(), DrawSpells = actor.DrawSpells.ToArray(), ElementDefenses = actor.ElementDefenses.ToArray() }).ToArray(), Items = source.Items.ToArray(), ActionDefinitions = source.ActionDefinitions.Select(action => action with { Statuses = action.Statuses?.ToArray() }).ToArray() };
    }

    private void ValidateDefinition(BattleDefinition source)
    {
        if (string.IsNullOrWhiteSpace(source.Name) || source.StageId < 0 || source.EncounterId < 0 || source.Combatants.Length is < 2 or > 16 || !source.Combatants.Any(actor => actor.IsPlayer) || !source.Combatants.Any(actor => !actor.IsPlayer))
        {
            throw new ArgumentException("Battle requires a name and both player and enemy combatants (2–16 total).");
        }
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<int> rewardSlots = [];
        foreach (BattleCombatantDefinition actor in source.Combatants)
        {
            bool invalidModel = actor.ModelId < -1 || actor.IsPlayer && actor.ModelId < 0 || actor.ModelId == -1 && (source.EncounterId is not null || actor.WeaponId is not null || actor.Costume != 0);
            if (string.IsNullOrWhiteSpace(actor.Id) || !ids.Add(actor.Id) || string.IsNullOrWhiteSpace(actor.Name) || invalidModel || actor.Costume < 0 || actor.MaximumHp is < 1 or > 1_000_000 || actor.CurrentHp < 0 || actor.CurrentHp > actor.MaximumHp || actor.Speed is < 1 or > AtbMaximum || actor.Attack is < 0 or > 1_000_000 || actor.Defense is < 0 or > 1_000_000 || actor.InitialAtb is < 0 or > AtbMaximum || actor.WeaponId < 0 || actor.EquipmentIds.Any(id => id < 0) || actor.Level is < 0 or > 100 || actor.Magic is < 0 or > 255 || actor.Spirit is < 0 or > 255 || actor.PhysicalPower is < 0 or > 255 || actor.ElementAttack is < 0 or > 255 || actor.ElementAttackPercent is < 0 or > 255 || actor.DrainPercent is < 0 or > 255 || actor.ElementDefenses.Length != 8 || actor.ElementDefenses.Any(value => value is < 0 or > 2550) || actor.DrawSpells.Any(spell => spell.Id is < 1 or >= 64 || spell.Resistance is < 0 or > 255) || actor.DrawSpells.Select(spell => spell.Id).Distinct().Count() != actor.DrawSpells.Length)
            {
                throw new ArgumentException("Invalid battle combatant identity, vitals, stats or equipment.");
            }
            if (actor.Abilities.Length == 0 || actor.Abilities.Any(ability => !Enum.IsDefined(ability)) || actor.Abilities.Distinct().Count() != actor.Abilities.Length)
            {
                throw new NotSupportedException("Battle command list contains an unsupported or duplicate command.");
            }
            ValidateStocks(actor.MagicStocks, 32);
            if (actor.Rewards is { } rewards)
            {
                rewards.Validate();
                if (actor.IsPlayer || actor.RewardSlot is not int slot || slot is < 0 or >= 8 || !rewardSlots.Add(slot) || actor.Level == 0)
                {
                    throw new InvalidDataException("Enemy reward definitions require their unique original formation slots and actual levels.");
                }
            }
            else if (actor.RewardSlot is not null)
            {
                throw new InvalidDataException("An enemy reward slot requires its original reward definition.");
            }
            HashSet<BattleStatusKind> statusKinds = [];
            foreach (BattleStatusEffectDefinition effect in actor.StatusEffects)
            {
                if (effect.DurationFrames is < 1 or > 1_000_000 || effect.TickIntervalFrames is < 1 or > 1_000_000 || effect.Magnitude is < 0 or > 1_000_000)
                {
                    throw new ArgumentException("Invalid battle status duration, interval or magnitude.");
                }
                if (!statusKinds.Add(effect.Kind))
                {
                    throw new ArgumentException("Duplicate battle status effect.");
                }
                if (!handlers.TryGetValue(effect.Kind, out IBattleStatusEffectHandler? handler))
                {
                    throw new NotSupportedException($"Battle status {effect.Kind} has no implemented handler.");
                }
                handler.Validate(effect);
            }
        }
        ValidateStocks(source.Items, 198);
        if (source.ActionDefinitions.Any(action => !Enum.IsDefined(action.Ability) || !Enum.IsDefined(action.Effect) || action.Id < 0 || string.IsNullOrWhiteSpace(action.Name) || action.Power < 0 || action.Statuses?.Any(kind => !handlers.ContainsKey(kind)) == true) || source.ActionDefinitions.GroupBy(action => (action.Ability, action.Id)).Any(group => group.Count() != 1))
        {
            throw new InvalidDataException("Invalid or unsupported battle action catalog.");
        }
    }

    private void RestoreValidated(BattleSnapshot snapshot)
    {
        if (snapshot.Version != 1 || snapshot.Frame < 0 || !Enum.IsDefined(snapshot.Outcome) || snapshot.Combatants is null || snapshot.Combatants.Length != combatants.Count || snapshot.QueuedActions is null || snapshot.Events is null)
        {
            throw new InvalidDataException("Invalid battle snapshot envelope.");
        }
        if (!snapshot.CustomHandlers.Order().SequenceEqual(handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler).Select(handler => handler.Kind).Order()))
        {
            throw new NotSupportedException("Battle snapshot requires its explicit custom effect handlers.");
        }
        HashSet<string> restored = new(StringComparer.Ordinal);
        foreach (BattleCombatantSnapshot saved in snapshot.Combatants)
        {
            BattleCombatant actor = GetActor(saved.Id);
            if (!restored.Add(saved.Id) || saved.CurrentHp < 0 || saved.CurrentHp > actor.MaximumHp || saved.Atb is < 0 or > AtbMaximum || (saved.CurrentHp == 0 && saved.Atb != 0) || saved.StatusEffects is null)
            {
                throw new InvalidDataException("Invalid battle combatant state.");
            }
            Dictionary<BattleStatusKind, BattleStatusState> initial = actor.Effects.ToDictionary(effect => effect.Definition.Kind);
            actor.Effects.Clear();
            HashSet<BattleStatusKind> kinds = [];
            foreach (BattleStatusSnapshot status in saved.StatusEffects)
            {
                BattleStatusEffectDefinition? effectDefinition = status.Definition ?? initial.GetValueOrDefault(status.Kind)?.Definition;
                if (effectDefinition is null || !handlers.ContainsKey(status.Kind) || effectDefinition.Kind != status.Kind || !kinds.Add(status.Kind) || status.RemainingFrames < 1 || status.RemainingFrames > effectDefinition.DurationFrames || status.UntilTickFrames < 1 || status.UntilTickFrames > effectDefinition.TickIntervalFrames)
                {
                    throw new InvalidDataException("Invalid battle status timer.");
                }
                int elapsedFrames = effectDefinition.DurationFrames - status.RemainingFrames;
                int expectedUntilTick = effectDefinition.TickIntervalFrames - elapsedFrames % effectDefinition.TickIntervalFrames;
                if (!effectDefinition.Permanent && status.UntilTickFrames != expectedUntilTick)
                {
                    throw new InvalidDataException("Battle status duration and periodic timer disagree.");
                }
                handlers[status.Kind].Validate(effectDefinition);
                actor.Effects.Add(new BattleStatusState(effectDefinition, status.RemainingFrames, status.UntilTickFrames));
            }
            actor.CurrentHp = saved.CurrentHp;
            actor.Atb = saved.Atb;
            ValidateStocks(saved.MagicStocks, 32);
            actor.Stocks.Clear();
            actor.Stocks.AddRange(saved.MagicStocks);
        }
        HashSet<string> busy = new(StringComparer.Ordinal);
        foreach (BattleAttackCommand command in snapshot.QueuedActions)
        {
            ValidateSavedAction(command.ActorId, command.TargetId, busy, command.Ability, command.ActionId, false);
            actions.Enqueue(command);
        }
        if (snapshot.ActiveAction is { } action)
        {
            int maximum = action.Phase == BattleActionPhase.Windup ? AttackWindupFrames : AttackRecoveryFrames;
            if (!Enum.IsDefined(action.Phase) || action.RemainingFrames < 1 || action.RemainingFrames > maximum)
            {
                throw new InvalidDataException("Invalid battle presentation timer.");
            }
            ValidateSavedAction(action.ActorId, action.TargetId, busy, action.Ability, action.ActionId, action.Phase == BattleActionPhase.Recovery);
        }
        if (snapshot.Outcome != DetermineOutcome() || (snapshot.Outcome != BattleOutcome.Active && busy.Count != 0))
        {
            throw new InvalidDataException("Battle outcome contradicts living actors or outstanding actions.");
        }
        long previousFrame = 0;
        foreach (BattleEvent item in snapshot.Events)
        {
            if (item.Frame < previousFrame || item.Frame > snapshot.Frame || string.IsNullOrWhiteSpace(item.Kind) || item.Message is null || item.Amount < 0 || (item.ActorId is not null && !byId.ContainsKey(item.ActorId)) || (item.TargetId is not null && !byId.ContainsKey(item.TargetId)))
            {
                throw new InvalidDataException("Invalid battle event log.");
            }
            previousFrame = item.Frame;
        }
        foreach (string id in busy)
        {
            byId[id].IsBusy = true;
        }
        Frame = snapshot.Frame;
        Outcome = snapshot.Outcome;
        ActiveAction = snapshot.ActiveAction;
        events.AddRange(snapshot.Events);
        ValidateStocks(snapshot.Items, 198);
        items.Clear();
        items.AddRange(snapshot.Items);
        if (snapshot.EnemyDefeats is null || snapshot.EnemyDefeats.Select(defeat => defeat.Slot).Distinct().Count() != snapshot.EnemyDefeats.Length)
        {
            throw new InvalidDataException("Battle snapshot lacks its unique observed enemy defeat facts.");
        }
        foreach (ReplayBattleEnemyDefeat defeat in snapshot.EnemyDefeats)
        {
            defeat.Validate();
            BattleCombatantDefinition source = definition.Combatants.SingleOrDefault(actor => actor.RewardSlot == defeat.Slot && actor.Rewards is not null) ?? throw new InvalidDataException("Observed enemy defeat has no installed reward owner.");
            if (byId[source.Id].IsAlive || source.Level != defeat.Level)
            {
                throw new InvalidDataException("Observed enemy defeat contradicts its actual combatant state.");
            }
        }
        enemyDefeats.AddRange(snapshot.EnemyDefeats);
        random = snapshot.Random ?? throw new InvalidDataException("Battle snapshot lacks its owned native RNG.");
        random.Validate();
    }

    private void ValidateSavedAction(string actorId, string targetId, HashSet<string> busy, BattleAbility ability, int actionId, bool allowDefeatedTarget)
    {
        BattleCombatant actor = GetActor(actorId);
        BattleCombatant target = GetActor(targetId);
        bool allies = ability != BattleAbility.Attack && ability != BattleAbility.Draw && FindAction(ability, actionId).Allies;
        if (!actor.IsAlive || actor.Atb != 0 || (actor.IsPlayer == target.IsPlayer) != allies || (!target.IsAlive && !allowDefeatedTarget && !IsRevive(ability, actionId)) || !busy.Add(actorId) || !actor.Abilities.Contains(ability))
        {
            throw new InvalidDataException("Invalid queued or active battle action.");
        }
    }

    private void PublishCandidate(BattleSession candidate)
    {
        foreach (BattleCombatant actor in combatants)
        {
            BattleCombatant replacement = candidate.byId[actor.Id];
            actor.CurrentHp = replacement.CurrentHp;
            actor.Atb = replacement.Atb;
            actor.IsBusy = replacement.IsBusy;
            actor.Effects.Clear();
            actor.Effects.AddRange(replacement.Effects);
            actor.Stocks.Clear();
            actor.Stocks.AddRange(replacement.Stocks);
        }
        actions.Clear();
        foreach (BattleAttackCommand command in candidate.actions)
        {
            actions.Enqueue(command);
        }
        events.Clear();
        events.AddRange(candidate.events);
        Frame = candidate.Frame;
        Outcome = candidate.Outcome;
        ActiveAction = candidate.ActiveAction;
        items.Clear();
        items.AddRange(candidate.items);
        random = candidate.random;
    }
}
