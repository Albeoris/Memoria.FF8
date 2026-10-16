using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum BattleNativeDuelPhase
{
    Opening,
    Ready,
    AwaitingEffect,
    Finisher
}

public sealed record BattleNativeDuelState(byte MoveSetId, ushort TargetMask, short InitialTimer, short RemainingTimer, BattleNativeDuelPhase Phase)
{
    public ImmutableArray<ushort> InputRing { get; init; } = Enumerable.Repeat((ushort)0, 8).ToImmutableArray();
    public byte InputCursor { get; init; }
    public short AutomaticCountdown { get; init; } = 120;
    public ushort PreviousButtons { get; init; }
    public ushort PendingPulse { get; init; }
    public long UiSubframeTicks { get; init; }
}

public sealed partial class BattleSession
{
    private readonly Dictionary<string, BattleNativeDuelState> nativeDuelProcesses = new(StringComparer.Ordinal);

    public BattleNativeDuelState? GetNativeDuel(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        return Outcome == BattleOutcome.Active && actor.IsAlive ? nativeDuelProcesses.GetValueOrDefault(actorId) : null;
    }

    public IReadOnlyList<BattleNativeDuelChoice> GetNativeDuelChoices(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        if (GetNativeDuel(actorId) is not { Phase: BattleNativeDuelPhase.Ready } state)
        {
            return [];
        }
        NativeLimitProgress progress = RequireNativeDuelProgress();
        return NativeBattleDuelRules.Choices(actor.NativeLimit!, state.MoveSetId, progress.DuelFlags, definition.NativeActions);
    }

    private NativeLimitProgress RequireNativeDuelProgress()
    {
        NativeLimitProgress progress = definition.LimitProgress ?? throw new NotSupportedException("Duel requires its saved learning flags and native options WORD1CFE978.");
        progress.Validate();
        if (progress.Options978 is null)
        {
            throw new NotSupportedException("Duel requires the actual saved native options WORD1CFE978.");
        }
        return progress;
    }

    private void QueueNativeDuelInvocation(BattleCombatant actor, BattleCombatant target, byte crisis)
    {
        _ = RequireNativeDuelProgress();
        ushort mask = NativeBattleLimitRules.PlayerTargetMask(actor.NativeLimit!.InvocationChoices.Single().TargetFlags, target.NativeIndex);
        OnNativePlayerCommandSubmitted(actor, 11);
        BattleAttackCommand command = new(actor.Id, target.Id, BattleAbility.LimitBreak, 11 << 16) { NativeQueuedCommand = 11, NativeTargetMask = mask, NativeDuel = new(crisis) };
        EnqueueNativePacket(actor, command, "Duel", playerInput: true);
    }

    private BattleNativeAction NativeDuelControl(BattleCombatant actor, byte crisis)
    {
        BattleNativeLimitDefinition rules = actor.NativeLimit ?? throw new InvalidDataException("Native Duel requires Zell's kernel command owner.");
        byte action = NativeBattleDuelRules.ActionId(rules, NativeBattleDuelRules.InitialMoveSet(rules, crisis));
        BattleNativeAction first = FindInstalledNativeAction(241, action);
        return first with { Command = 11, ActionId = 0, AttackType = 0, Power = 0, HitCount = 1, AttackFlags = 3, DamagePresentation = 0, Element = 0, Statuses0 = 0, Statuses1 = 0, CrisisLevel = crisis, Name = "Duel" };
    }

    private BattleAttackCommand PrepareNativeDuelInvocation(BattleCombatant actor, BattleAttackCommand command)
    {
        if (command.NativeDuel is not { } invocation)
        {
            return command;
        }
        byte crisis = invocation.CrisisLevel;
        NativeLimitProgress progress = RequireNativeDuelProgress();
        if (nativeDuelProcesses.Count != 0 || nativeShotProcesses.Count != 0 || nativeRenzokukenProcesses.Count != 0)
        {
            throw new InvalidOperationException("A native timed Limit input process already owns the battle menu.");
        }
        if ((progress.Options978!.Value & 2) != 0 && nativeBattleUiRandom is null)
        {
            throw new NotSupportedException("Automatic Duel requires its actual4A7080 battle UI random owner.");
        }
        BattleNativeLimitDefinition rules = actor.NativeLimit!;
        byte moveSet = NativeBattleDuelRules.InitialMoveSet(rules, crisis);
        byte action = NativeBattleDuelRules.ActionId(rules, moveSet);
        short timer = NativeBattleDuelRules.Timer(rules, crisis);
        if (timer == 0)
        {
            throw new InvalidDataException("Native Duel timer cannot be zero.");
        }
        ushort mask = command.NativeTargetMask;
        BattleNativeAction opening = FindInstalledNativeAction(241, action);
        BattleAttackCommand second = new(actor.Id, command.TargetId, BattleAbility.EnemyAbility, action) { NativeQueuedCommand = 241, NativeAction = opening, NativeTargetMask = mask, NativeLimitCallback = NativeLimitCallback.DuelOpening };
        if (!TryWriteNativeSecondCommand(second))
        {
            throw new InvalidDataException("Native48D200 Duel requires its selected physical command entry.");
        }
        nativeDuelProcesses.Add(actor.Id, new(moveSet, mask, timer, timer, BattleNativeDuelPhase.Opening));
        return command with { NativeAction = NativeDuelControl(actor, crisis) };
    }

    public void AdvanceNativeDuelUi(int frames = 1, ushort rawPulse = 0)
    {
        if (frames is < 1 or > 30000)
        {
            throw new ArgumentOutOfRangeException(nameof(frames));
        }
        for (int frame = 0; frame < frames; frame++)
        {
            foreach ((string actorId, BattleNativeDuelState state) in nativeDuelProcesses.ToArray())
            {
                if (GetNativeDuel(actorId) is null)
                {
                    EndNativeDuel(actorId);
                    continue;
                }
                if (state.Phase != BattleNativeDuelPhase.Ready)
                {
                    continue;
                }
                BattleNativeDuelState next = state with { RemainingTimer = (short)(state.RemainingTimer - 1), AutomaticCountdown = (short)(state.AutomaticCountdown - 1) };
                nativeDuelProcesses[actorId] = next;
                if (next.RemainingTimer <= 0)
                {
                    EndNativeDuel(actorId);
                    continue;
                }
                IReadOnlyList<BattleNativeDuelChoice> choices = GetNativeDuelChoices(actorId);
                if ((RequireNativeDuelProgress().Options978!.Value & 2) != 0)
                {
                    if (next.AutomaticCountdown == 30)
                    {
                        BattleNativeDuelChoice[] learned = choices.Where(choice => choice.Learned).ToArray();
                        BattleNativeDuelChoice selected = choices[0];
                        if (learned.Length != 0)
                        {
                            nativeBattleUiRandom = (nativeBattleUiRandom ?? throw new NotSupportedException("Automatic Duel requires its actual4A7080 battle UI random owner.")).Draw(out ushort roll);
                            int index = roll / (0x8000 / learned.Length);
                            if (index >= learned.Length)
                            {
                                throw new NotSupportedException("Native4AF840 automatic Duel reads an uninitialized fourth stack byte for this4A7080 roll.");
                            }
                            selected = learned[index];
                        }
                        QueueNativeDuelChoice(actorId, selected);
                    }
                    continue;
                }
                if (frame != 0 || rawPulse == 0)
                {
                    continue;
                }
                ushort[] ring = next.InputRing.ToArray();
                byte cursor = (byte)((next.InputCursor + 1) & 7);
                ring[cursor] = rawPulse;
                next = next with { InputRing = ring.ToImmutableArray(), InputCursor = cursor };
                nativeDuelProcesses[actorId] = next;
                BattleNativeDuelChoice? match = choices.FirstOrDefault(choice => NativeBattleDuelRules.Matches(choice, next.InputRing, cursor));
                if (match is not null)
                {
                    QueueNativeDuelChoice(actorId, match);
                }
            }
        }
    }

    private void QueueNativeDuelChoice(string actorId, BattleNativeDuelChoice choice)
    {
        BattleNativeDuelState state = nativeDuelProcesses[actorId];
        BattleCombatant actor = GetActor(actorId);
        byte command = choice.Finisher ? (byte)239 : (byte)241;
        BattleNativeAction action = FindInstalledNativeAction(command, choice.ActionId);
        byte flags = action.TargetFlags!.Value;
        ushort mask = choice.Finisher && (flags & 0x30) != 0x10 ? NativeAutomaticActionTargetMask(flags) : state.TargetMask;
        QueueNativeCommand(actor, command, choice.ActionId, 0, mask, callback: choice.Finisher ? NativeLimitCallback.DuelFinisher : NativeLimitCallback.DuelEffect, playerInput: true);
        nativeDuelProcesses[actorId] = state with { MoveSetId = choice.MoveSetId, Phase = choice.Finisher ? BattleNativeDuelPhase.Finisher : BattleNativeDuelPhase.AwaitingEffect };
    }

    private void EndNativeDuel(string actorId)
    {
        nativeDuelProcesses.Remove(actorId);
        nativeLimitLevels.Remove(actorId);
        BattleCombatant actor = GetActor(actorId);
        actor.IsBusy = QueuedActions.Any(command => command.ActorId == actorId) || ActiveAction?.ActorId == actorId;
        //TODO(TD-009): Native241/65532 closes through its graphics end sequence; the current UI owner closes at this boundary
        Log("NativeLimitControl", actorId, null, 65532, "Duel ended.");
    }

    private bool CompleteNativeDuelAction(BattleActionPresentation action)
    {
        if (!nativeDuelProcesses.TryGetValue(action.ActorId, out BattleNativeDuelState? state))
        {
            return false;
        }
        if (action.NativeLimitCallback == NativeLimitCallback.DuelFinisher)
        {
            EndNativeDuel(action.ActorId);
            return false;
        }
        if (action.NativeLimitCallback is NativeLimitCallback.DuelOpening or NativeLimitCallback.DuelEffect)
        {
            //TODO(TD-009): Native47E200 dispatches48E5A0/48E5E0 UI readiness through graphics command10
            nativeDuelProcesses[action.ActorId] = state with { Phase = BattleNativeDuelPhase.Ready, InputRing = Enumerable.Repeat((ushort)0, 8).ToImmutableArray(), InputCursor = 0, AutomaticCountdown = 120, PendingPulse = 0 };
        }
        return true;
    }

    public void AdvanceNativeDuelPresentation(TimeSpan elapsed, ushort heldButtons)
    {
        if (elapsed < TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(1000))
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }
        if (elapsed == TimeSpan.Zero || nativeDuelProcesses.Count == 0)
        {
            return;
        }
        (string actorId, BattleNativeDuelState state) = nativeDuelProcesses.Single();
        ushort pulse = (ushort)(heldButtons & ~state.PreviousButtons);
        ushort pending = state.Phase == BattleNativeDuelPhase.Ready ? (ushort)(state.PendingPulse | pulse) : (ushort)0;
        //TODO(TD-009): Native UI callbacks currently use the host30Hz presentation clock; preserve its fractional owner independently of gameplay ticks
        long ticks = state.UiSubframeTicks + elapsed.Ticks * FramesPerSecond;
        int frames = checked((int)(ticks / TimeSpan.TicksPerSecond));
        nativeDuelProcesses[actorId] = state with { PreviousButtons = heldButtons, PendingPulse = frames == 0 ? pending : (ushort)0, UiSubframeTicks = ticks % TimeSpan.TicksPerSecond };
        if (frames != 0)
        {
            AdvanceNativeDuelUi(frames, pending);
        }
    }

    private void RestoreNativeDuel(string actorId, byte crisis, BattleNativeDuelState state)
    {
        BattleCombatant actor = GetActor(actorId);
        NativeLimitProgress progress = RequireNativeDuelProgress();
        if ((progress.Options978!.Value & 2) != 0 && definition.NativeBattleUiRandom is null)
        {
            throw new InvalidDataException("Saved automatic Duel lacks its actual4A7080 battle UI random owner.");
        }
        if (Outcome != BattleOutcome.Active || !actor.IsAlive || actor.NativeLimit is not { Command: 11 } rules || crisis == 0 || state.InitialTimer != NativeBattleDuelRules.Timer(rules, crisis) || state.InitialTimer == 0 || state.RemainingTimer is <= 0 || state.RemainingTimer > state.InitialTimer || !Enum.IsDefined(state.Phase) || state.InputRing.IsDefault || state.InputRing.Length != 8 || state.InputCursor >= 8 || state.AutomaticCountdown > 120 || state.UiSubframeTicks is < 0 or >= TimeSpan.TicksPerSecond)
        {
            throw new InvalidDataException("Saved Duel process contradicts its native character, timer or raw input ring.");
        }
        _ = NativeBattleDuelRules.ActionId(rules, state.MoveSetId);
        nativeDuelProcesses.Add(actorId, state);
        actor.IsBusy = true;
        NativeLimitCallback? expected = state.Phase switch { BattleNativeDuelPhase.Opening => NativeLimitCallback.DuelOpening, BattleNativeDuelPhase.AwaitingEffect => NativeLimitCallback.DuelEffect, BattleNativeDuelPhase.Finisher => NativeLimitCallback.DuelFinisher, _ => null };
        RequireNativeLimitCallback(actorId, expected);
    }
}
