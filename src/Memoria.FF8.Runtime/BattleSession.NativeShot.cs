namespace Memoria.FF8.Runtime;

public enum BattleNativeShotPhase
{
    Opening,
    Ready,
    AwaitingEffect,
    Ending
}

public sealed record BattleNativeShotState(byte ActionId, BattleTargetRequest TargetRequest, short InitialTimer, short RemainingTimer, byte RemainingAmmo, BattleNativeShotPhase Phase)
{
    public long UiSubframeTicks { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public BattleTimedInputUi? Ui { get; init; }
}

public sealed partial class BattleSession
{
    private readonly Dictionary<string, BattleNativeShotState> nativeShotProcesses = new(StringComparer.Ordinal);

    public BattleNativeShotState? GetNativeShot(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        return Outcome == BattleOutcome.Active && actor.IsAlive ? nativeShotProcesses.GetValueOrDefault(actorId) : null;
    }

    public BattleTimedInputUi? GetShotUi(string actorId) => GetNativeShot(actorId) is { } state ? ShotUi(state) : null;

    public IReadOnlyList<BattleCombatant> GetNativeShotTargets(string actorId)
    {
        return GetNativeShot(actorId) is { } state ? ResolveBattleTargets(state.TargetRequest, sampleRandom: false) : [];
    }

    private static BattleTimedInputUi ShotUi(BattleNativeShotState state) => state.Ui ?? new(state.Phase == BattleNativeShotPhase.Ending ? BattleTimedInputUiPhase.Closing : state.Phase == BattleNativeShotPhase.Opening ? BattleTimedInputUiPhase.Opening : BattleTimedInputUiPhase.Active);

    private IEnumerable<BattleNativeAction> GetNativeShotChoices()
    {
        NativeShotAvailabilityState progress = definition.NativeShotAvailability ?? throw new NotSupportedException("Shot menu requires the saved BYTE1CFE770 learned owner.");
        progress.Validate();
        byte flags = nativeScriptState.ShotFlagsAfter ?? progress.Flags770;
        for (ushort action = 0; action < 8; action++)
        {
            if ((flags & (1 << action)) != 0 || nativeItems.Any(stock => stock.ItemId == action + 101 && stock.Count != 0))
            {
                yield return FindInstalledNativeAction(14, action);
            }
        }
    }

    private void BeginNativeShot(BattleCombatant actor, BattleCombatant target, BattleNativeAction action, byte crisis)
    {
        if (nativeShotProcesses.Count != 0 || nativeDuelProcesses.Count != 0 || nativeRenzokukenProcesses.Count != 0 || HasQueuedExpandedTimedLimit || actor.NativeLimit!.ShotTimers.IsDefault || actor.NativeLimit.ShotTimers.Length != 4)
        {
            throw new InvalidDataException("Shot invocation requires one process and all four original kernel29 timers.");
        }
        NativeItemStock? ammo = nativeItems.FirstOrDefault(stock => stock.ItemId == action.ActionId + 101 && stock.Count != 0);
        if (ammo is null)
        {
            throw new InvalidOperationException("The selected Shot ammunition is exhausted.");
        }
        short timer = (short)(4 * actor.NativeLimit.ShotTimers[crisis - 1]);
        if (timer == 0)
        {
            throw new InvalidDataException("Original Shot timer cannot be zero.");
        }
        byte flags = (action.TargetFlags!.Value & 0x10) != 0 ? (byte)0x50 : (byte)0x40;
        if (UsesExpandedRoster)
        {
            if (!CanSelectExpandedShotTarget(target) || definition.NativeShotAvailability is null)
            {
                throw new InvalidOperationException("Shot requires a living enemy and its installed ammunition owner.");
            }
            QueueExpandedShot(actor, target.Id, action, NativeLimitCallback.ShotOpening);
            nativeShotProcesses.Add(actor.Id, new((byte)action.ActionId, SelectBattleTarget(target, eligibility: BattleTargetEligibility.Living), timer, timer, ammo.Count, BattleNativeShotPhase.Opening)
            {
                Ui = new(BattleTimedInputUiPhase.Opening)
            });
            return;
        }
        BattleTargetRequest request = CreatePlayerTargetRequest(actor, target, flags);
        QueueNativeShotAction(actor, action, request, NativeLimitCallback.ShotOpening);
        nativeShotProcesses.Add(actor.Id, new((byte)action.ActionId, request, timer, timer, ammo.Count, BattleNativeShotPhase.Opening) { Ui = new(BattleTimedInputUiPhase.Opening) });
    }

    public void AdvanceNativeShotUi(int frames = 1)
    {
        if (frames is < 1 or > 30000)
        {
            throw new ArgumentOutOfRangeException(nameof(frames));
        }
        for (int frame = 0; frame < frames; frame++)
        {
            foreach ((string actorId, BattleNativeShotState previous) in nativeShotProcesses.ToArray())
            {
                BattleNativeShotState state = previous with { Ui = ShotUi(previous).Advance(FramesPerSecond) };
                nativeShotProcesses[actorId] = state;
                if (Outcome != BattleOutcome.Active || !GetActor(actorId).IsAlive)
                {
                    nativeShotProcesses.Remove(actorId);
                    nativeLimitLevels.Remove(actorId);
                    continue;
                }
                if (state.Phase is BattleNativeShotPhase.Opening or BattleNativeShotPhase.Ending)
                {
                    continue;
                }
                short remaining = (short)(state.RemainingTimer - 1);
                nativeShotProcesses[actorId] = state with { RemainingTimer = remaining };
                if (remaining == 0)
                {
                    EndNativeShot(actorId);
                }
            }
        }
    }

    public void AdvanceNativeShotPresentation(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(1000))
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }
        if (nativeShotProcesses.Count == 0)
        {
            return;
        }
        (string actorId, BattleNativeShotState state) = nativeShotProcesses.Single();
        //The host presentation API is the single timed-input clock in both graphics modes.
        long ticks = state.UiSubframeTicks + elapsed.Ticks * FramesPerSecond;
        int frames = checked((int)(ticks / TimeSpan.TicksPerSecond));
        nativeShotProcesses[actorId] = state with { UiSubframeTicks = ticks % TimeSpan.TicksPerSecond };
        if (frames != 0)
        {
            AdvanceNativeShotUi(frames);
        }
    }

    public bool TriggerNativeShot(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        if (GetNativeShot(actorId) is not { Phase: BattleNativeShotPhase.Ready, RemainingTimer: > 0, RemainingAmmo: > 0 } state)
        {
            return false;
        }
        BattleNativeAction action = FindInstalledNativeAction(237, state.ActionId);
        ValidateNativeActionContract(action);
        if (UsesExpandedRoster && !enemies.Any(CanSelectExpandedShotTarget))
        {
            EndNativeShot(actorId);
            return false;
        }
        _ = ConsumeNativeItem(state.ActionId + 101);
        nativeShotProcesses[actorId] = state with { RemainingAmmo = (byte)(state.RemainingAmmo - 1), Phase = BattleNativeShotPhase.AwaitingEffect };
        if (UsesExpandedRoster)
        {
            QueueExpandedShot(actor, ReadExpandedShotTarget(state), action, NativeLimitCallback.ShotEffect);
        }
        else
        {
            QueueNativeShotAction(actor, action, state.TargetRequest, NativeLimitCallback.ShotEffect);
        }
        return true;
    }

    public void EndNativeShot(string actorId)
    {
        if (GetNativeShot(actorId) is not { } state || state.Phase is BattleNativeShotPhase.Opening or BattleNativeShotPhase.Ending)
        {
            return;
        }
        BattleNativeAction ending = FindInstalledNativeAction(238, state.ActionId);
        ValidateNativeActionContract(ending);
        nativeShotProcesses[actorId] = state with { Phase = BattleNativeShotPhase.Ending, RemainingTimer = -1, Ui = new(BattleTimedInputUiPhase.Closing) };
        if (UsesExpandedRoster)
        {
            QueueExpandedShot(GetActor(actorId), ReadExpandedShotTarget(state), ending, NativeLimitCallback.ShotEnding);
        }
        else
        {
            QueueNativeShotAction(GetActor(actorId), ending, new(new BattleTargetSelection.SideGroup(BattleTargetSide.Enemies), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.UseCurrentBattlePolicy), NativeLimitCallback.ShotEnding);
        }
    }

    private bool CompleteNativeShotAction(BattleActionPresentation action)
    {
        if (!nativeShotProcesses.TryGetValue(action.ActorId, out BattleNativeShotState? state))
        {
            return false;
        }
        if (action.NativeLimitCallback == NativeLimitCallback.ShotEnding || action.NativeLimitCallback == NativeLimitCallback.ShotOpening && action.NativeQueuedCommand != 14)
        {
            nativeShotProcesses.Remove(action.ActorId);
            nativeLimitLevels.Remove(action.ActorId);
            return false;
        }
        //TODO(TD-009): Graphicsless components project48D1A0/4AD7C0 at action completion; native descriptors own raw readiness. The remaining zero-ammo widget close still uses this completion boundary.
        if (action.NativeLimitCallback is NativeLimitCallback.ShotOpening or NativeLimitCallback.ShotEffect && state.Phase != BattleNativeShotPhase.Ending)
        {
            if (UsesExpandedRoster && !enemies.Any(CanSelectExpandedShotTarget))
            {
                // A control opening can finish after an already queued action defeated its final recipient.
                nativeShotProcesses[action.ActorId] = state with { Phase = BattleNativeShotPhase.Ready };
                EndNativeShot(action.ActorId);
            }
            else if (state.RemainingAmmo == 0)
            {
                EndNativeShot(action.ActorId);
            }
            else if (NativeGraphics is null && !OwnsExpandedHostedInvocation)
            {
                nativeShotProcesses[action.ActorId] = state with { Phase = BattleNativeShotPhase.Ready };
            }
        }
        return true;
    }

    private void RestoreNativeShot(string actorId, byte crisis, BattleNativeShotState state)
    {
        BattleCombatant actor = GetActor(actorId);
        if (state.TargetRequest is null || UsesExpandedRoster && (state.TargetRequest.Selection is not BattleTargetSelection.Explicit { Actors: [BattleEnemyActorReference target] } || FindTargetActor(target) is null))
        {
            throw new InvalidDataException("Saved Shot process lacks its original host's target owner.");
        }
        if (UsesExpandedRoster && nativeItems.Where(stock => stock.ItemId == state.ActionId + 101).Sum(stock => stock.Count) != state.RemainingAmmo)
        {
            throw new InvalidDataException("Saved expanded Shot ammunition differs from its inventory owner.");
        }
        if (Outcome != BattleOutcome.Active || !actor.IsAlive || actor.NativeLimit is not { Command: 14 } rules || rules.ShotTimers.IsDefault || rules.ShotTimers.Length != 4 || crisis == 0 || state.ActionId >= 8 || state.InitialTimer != 4 * rules.ShotTimers[crisis - 1] || state.InitialTimer == 0 || state.RemainingTimer < -1 || state.RemainingTimer > state.InitialTimer || state.RemainingAmmo > 100 || state.UiSubframeTicks is < 0 or >= TimeSpan.TicksPerSecond || !Enum.IsDefined(state.Phase) || state.Phase == BattleNativeShotPhase.Ready && state.RemainingTimer <= 0)
        {
            string timers = actor.NativeLimit is { } limit && !limit.ShotTimers.IsDefault ? string.Join("/", limit.ShotTimers) : "missing";
            throw new InvalidDataException($"Saved Shot process contradicts its native character, timer or ammunition owner: outcome={Outcome}, active={actor.IsActive}, alive={actor.IsAlive}, savedStatus={NativeSavedStatusBits(actor):X4}, command={actor.NativeLimit?.Command}, timers={timers}, crisis={crisis}, action={state.ActionId}, initial={state.InitialTimer}, remaining={state.RemainingTimer}, ammo={state.RemainingAmmo}, phase={state.Phase}, definedPhase={Enum.IsDefined(state.Phase)}, uiTicks={state.UiSubframeTicks}.");
        }
        nativeShotProcesses.Add(actorId, state);
        actor.IsBusy = true;
        NativeLimitCallback? expected = state.Phase switch { BattleNativeShotPhase.Opening => NativeLimitCallback.ShotOpening, BattleNativeShotPhase.AwaitingEffect => NativeLimitCallback.ShotEffect, BattleNativeShotPhase.Ending => NativeLimitCallback.ShotEnding, _ => null };
        RequireNativeLimitCallback(actorId, expected);
    }

    private static bool IsExpandedShotAction(BattleNativeAction? action)
    {
        return action is { Command: 14 or 237 or 238, KernelSection: 21, HitCount: 1, AttackType: 1 or 36, TargetFlags: byte flags } &&
            (flags & 0x70) is 0x40 or 0x50;
    }

    private static bool IsShotCallback(NativeLimitCallback? callback)
    {
        return callback is NativeLimitCallback.ShotOpening or NativeLimitCallback.ShotEffect or NativeLimitCallback.ShotEnding;
    }

    private static bool CanSelectExpandedShotTarget(BattleCombatant actor)
    {
        return !actor.IsPlayer && actor.IsAlive && !actor.Untargetable;
    }

    private void QueueExpandedShot(BattleCombatant actor, string targetId, BattleNativeAction action, NativeLimitCallback callback)
    {
        BattleCombatant target = GetActor(targetId);
        BattleTargetRequest request = SelectBattleTarget(target, eligibility: BattleTargetEligibility.Living);
        if (callback == NativeLimitCallback.ShotEnding && OwnsExpandedHostedInvocation)
        {
            request = new(new BattleTargetSelection.SideGroup(BattleTargetSide.Enemies), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.UseCurrentBattlePolicy);
        }
        else if (callback == NativeLimitCallback.ShotEffect || callback == NativeLimitCallback.ShotOpening && OwnsExpandedHostedInvocation)
        {
            BattleTargetSelection selection = (action.TargetFlags!.Value & 0x10) == 0
                ? new BattleTargetSelection.SideGroup(BattleTargetSide.Enemies)
                : request.Selection;
            request = new(selection, BattleTargetEligibility.Living, false, BattleTargetReplacement.UseCurrentBattlePolicy);
        }
        actions.Enqueue(new(actor.Id, targetId, request, BattleAbility.LimitBreak, action.Command << 16 | action.ActionId)
        {
            NativeAction = action,
            NativeQueuedCommand = action.Command,
            NativeLimitCallback = callback
        });
        actor.Atb = 0;
        actor.IsBusy = true;
        Log("Queued", actor.Id, targetId, 0, $"{actor.Name}: {action.Name} queued.");
    }

    private void QueueNativeShotAction(BattleCombatant actor, BattleNativeAction action, BattleTargetRequest request, NativeLimitCallback callback)
    {
        BattleCombatant target = ResolveBattleTargets(request, sampleRandom: false).FirstOrDefault() ?? actor;
        BattleAttackCommand command = new(actor.Id, target.Id, request, BattleAbility.EnemyAbility, action.ActionId)
        {
            NativeAction = action,
            NativeLimitCallback = callback,
            NativeQueuedCommand = action.Command,
            NativeSequence = 0
        };
        EnqueueNativePacket(actor, command, action.Name ?? "Shot", playerInput: true);
    }

    private static string ReadExpandedShotTarget(BattleNativeShotState state)
    {
        return state.TargetRequest.Selection is BattleTargetSelection.Explicit { Actors: [BattleEnemyActorReference target] }
            ? target.ActorId
            : throw new InvalidDataException("Expanded Shot requires its selected enemy target.");
    }

    private bool TryTakeHostedShotCommand(string actorId, out BattleAttackCommand command)
    {
        BattleAttackCommand? selected = null;
        for (int remaining = actions.Count; remaining > 0; remaining--)
        {
            BattleAttackCommand pending = actions.Dequeue();
            if (selected is null && pending.ActorId == actorId && IsShotCallback(pending.NativeLimitCallback))
            {
                selected = pending;
            }
            else
            {
                actions.Enqueue(pending);
            }
        }
        command = selected!;
        return selected is not null;
    }

    private void ReconcileExpandedShot()
    {
        if (!UsesExpandedRoster || nativeShotProcesses.Count == 0)
        {
            return;
        }
        (string actorId, BattleNativeShotState state) = nativeShotProcesses.Single();
        if (!CanNativeBattleActorAct(GetActor(actorId)))
        {
            nativeShotProcesses.Remove(actorId);
            nativeLimitLevels.Remove(actorId);
        }
        else if (state.Phase == BattleNativeShotPhase.Ready && !enemies.Any(CanSelectExpandedShotTarget))
        {
            EndNativeShot(actorId);
        }
    }
}
