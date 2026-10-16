namespace Memoria.FF8.Runtime;

public enum BattleNativeShotPhase
{
    Opening,
    Ready,
    AwaitingEffect,
    Ending
}

public sealed record BattleNativeShotState(byte ActionId, ushort TargetMask, short InitialTimer, short RemainingTimer, byte RemainingAmmo, BattleNativeShotPhase Phase)
{
    public long UiSubframeTicks { get; init; }
}

public sealed partial class BattleSession
{
    private readonly Dictionary<string, BattleNativeShotState> nativeShotProcesses = new(StringComparer.Ordinal);

    public BattleNativeShotState? GetNativeShot(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        return Outcome == BattleOutcome.Active && actor.IsAlive ? nativeShotProcesses.GetValueOrDefault(actorId) : null;
    }

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
        if (nativeShotProcesses.Count != 0 || nativeDuelProcesses.Count != 0 || actor.NativeLimit!.ShotTimers.IsDefault || actor.NativeLimit.ShotTimers.Length != 4)
        {
            throw new InvalidDataException("Shot invocation requires one process and all four original kernel29 timers.");
        }
        NativeItemStock ammo = nativeItems.First(stock => stock.ItemId == action.ActionId + 101 && stock.Count != 0);
        short timer = (short)(4 * actor.NativeLimit.ShotTimers[crisis - 1]);
        if (timer == 0)
        {
            throw new InvalidDataException("Original Shot timer cannot be zero.");
        }
        byte flags = (action.TargetFlags!.Value & 0x10) != 0 ? (byte)0x50 : (byte)0x40;
        ushort mask = NativeBattleLimitRules.PlayerTargetMask(flags, target.NativeIndex);
        QueueNativeCommand(actor, 14, action.ActionId, 0, mask, crisis, NativeLimitCallback.ShotOpening);
        nativeShotProcesses.Add(actor.Id, new((byte)action.ActionId, mask, timer, timer, ammo.Count, BattleNativeShotPhase.Opening));
    }

    public void AdvanceNativeShotUi(int frames = 1)
    {
        if (frames is < 1 or > 30000)
        {
            throw new ArgumentOutOfRangeException(nameof(frames));
        }
        for (int frame = 0; frame < frames; frame++)
        {
            foreach ((string actorId, BattleNativeShotState state) in nativeShotProcesses.ToArray())
            {
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
        if (state.Phase is BattleNativeShotPhase.Opening or BattleNativeShotPhase.Ending)
        {
            return;
        }
        //TODO(TD-009): Project the original active Shot widget callbacks onto the host's30Hz presentation clock until native UI/effect scheduling is owned
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
        _ = ConsumeNativeItem(state.ActionId + 101);
        nativeShotProcesses[actorId] = state with { RemainingAmmo = (byte)(state.RemainingAmmo - 1), Phase = BattleNativeShotPhase.AwaitingEffect };
        QueueNativeCommand(actor, 237, state.ActionId, 0, state.TargetMask, callback: NativeLimitCallback.ShotEffect);
        return true;
    }

    public void EndNativeShot(string actorId)
    {
        if (GetNativeShot(actorId) is not { } state || state.Phase is BattleNativeShotPhase.Opening or BattleNativeShotPhase.Ending)
        {
            return;
        }
        ValidateNativeActionContract(FindInstalledNativeAction(238, state.ActionId));
        nativeShotProcesses[actorId] = state with { Phase = BattleNativeShotPhase.Ending, RemainingTimer = -1 };
        QueueNativeCommand(GetActor(actorId), 238, state.ActionId, 0, 0x80F8, callback: NativeLimitCallback.ShotEnding);
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
        //TODO(TD-009): Native47E200 graphics command10 owns4AD7C0 readiness; use the current action completion projection until that VM is owned
        if (action.NativeLimitCallback is NativeLimitCallback.ShotOpening or NativeLimitCallback.ShotEffect && state.Phase != BattleNativeShotPhase.Ending)
        {
            if (state.RemainingAmmo == 0)
            {
                EndNativeShot(action.ActorId);
            }
            else
            {
                nativeShotProcesses[action.ActorId] = state with { Phase = BattleNativeShotPhase.Ready };
            }
        }
        return true;
    }

    private void RestoreNativeShot(string actorId, byte crisis, BattleNativeShotState state)
    {
        BattleCombatant actor = GetActor(actorId);
        if (Outcome != BattleOutcome.Active || !actor.IsAlive || actor.NativeLimit is not { Command: 14 } rules || rules.ShotTimers.IsDefault || rules.ShotTimers.Length != 4 || crisis == 0 || state.ActionId >= 8 || state.InitialTimer != 4 * rules.ShotTimers[crisis - 1] || state.InitialTimer == 0 || state.RemainingTimer < -1 || state.RemainingTimer > state.InitialTimer || state.RemainingAmmo > 100 || state.UiSubframeTicks is < 0 or >= TimeSpan.TicksPerSecond || !Enum.IsDefined(state.Phase) || state.Phase == BattleNativeShotPhase.Ready && state.RemainingTimer <= 0)
        {
            throw new InvalidDataException("Saved Shot process contradicts its native character, timer or ammunition owner.");
        }
        nativeShotProcesses.Add(actorId, state);
        actor.IsBusy = true;
        NativeLimitCallback? expected = state.Phase switch { BattleNativeShotPhase.Opening => NativeLimitCallback.ShotOpening, BattleNativeShotPhase.AwaitingEffect => NativeLimitCallback.ShotEffect, BattleNativeShotPhase.Ending => NativeLimitCallback.ShotEnding, _ => null };
        RequireNativeLimitCallback(actorId, expected);
    }
}
