using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum BattleNativeDuelPhase
{
    Opening,
    Ready,
    AwaitingEffect,
    Finisher
}

public sealed record BattleNativeDuelState(byte MoveSetId, BattleTargetRequest TargetRequest, short InitialTimer, short RemainingTimer, BattleNativeDuelPhase Phase)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public byte? Selection1D28DEF { get; init; }
    public ImmutableArray<ushort> InputRing { get; init; } = Enumerable.Repeat((ushort)0, 8).ToImmutableArray();
    public byte InputCursor { get; init; }
    public short AutomaticCountdown { get; init; } = 120;
    public ushort PreviousButtons { get; init; }
    public ushort PendingPulse { get; init; }
    public byte WidgetPhase { get; init; }
    public byte PublicationLatch { get; init; }
    public ushort WidgetRevealQ12 { get; init; }
    public short ClosingCountdown { get; init; }
    public bool WidgetClosed => WidgetPhase == 13;
    public bool GameplayFinished { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public BattleTimedInputUi? Ui { get; init; }
}

public sealed partial class BattleSession
{
    public BattleNativeDuelState? GetNativeDuel(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        return Outcome == BattleOutcome.Active && actor.IsAlive ? nativeDuelProcesses.GetValueOrDefault(actorId) : null;
    }

    public BattleTimedInputUi? GetDuelUi(string actorId) => GetNativeDuel(actorId) is { } state ? DuelUi(state) : null;

    private static BattleTimedInputUi DuelUi(BattleNativeDuelState state) => state.Ui ?? new(state.Phase == BattleNativeDuelPhase.Finisher ? BattleTimedInputUiPhase.Closing : state.Phase == BattleNativeDuelPhase.Opening ? BattleTimedInputUiPhase.Opening : BattleTimedInputUiPhase.Active);

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
            QueueExpandedDuelInvocation(actor, target, crisis);
            return;
    }

    private BattleNativeAction NativeDuelControl(BattleCombatant actor, byte crisis)
    {
        BattleNativeLimitDefinition rules = actor.NativeLimit ?? throw new InvalidDataException("Native Duel requires Zell's kernel command owner.");
        byte action = NativeBattleDuelRules.ActionId(rules, NativeBattleDuelRules.InitialMoveSet(rules, crisis));
        BattleNativeAction first = FindInstalledNativeAction(241, action);
        return first with { Command = 11, ActionId = 0, AttackType = 0, Power = 0, HitCount = 1, AttackFlags = 3, DamagePresentation = 0, Element = 0, Statuses0 = 0, Statuses1 = 0, CrisisLevel = crisis, Name = "Duel" };
    }

    private void QueueNativeDuelCloseSelection(string actorId)
    {
        BattleNativeDuelState state = nativeDuelProcesses[actorId];
        if (state.Phase != BattleNativeDuelPhase.Finisher)
        {
            nativeDuelProcesses[actorId] = NativeBattleDuelRules.RequestWidgetClose(state);
        }
    }

    private void EndNativeDuel(string actorId)
    {
        if (nativeDuelProcesses.TryGetValue(actorId, out BattleNativeDuelState? state) && !state.WidgetClosed)
        {
            nativeDuelProcesses[actorId] = state with { GameplayFinished = true };
            return;
        }
        nativeDuelProcesses.Remove(actorId);
        nativeLimitLevels.Remove(actorId);
        BattleCombatant actor = GetActor(actorId);
        actor.IsBusy = QueuedActions.Any(command => command.ActorId == actorId) || ActiveAction?.ActorId == actorId;
        Log("NativeLimitControl", actorId, null, 65532, "Duel ended.");
    }

    private bool CompleteNativeDuelAction(BattleActionPresentation action)
    {
        if (!nativeDuelProcesses.ContainsKey(action.ActorId))
        {
            return false;
        }
        if (action.NativeLimitCallback == NativeLimitCallback.DuelFinisher)
        {
            EndNativeDuel(action.ActorId);
            return nativeDuelProcesses.ContainsKey(action.ActorId);
        }
        return true;
    }

    public void SubmitNativeDuelInput(ushort heldButtons)
    {
        if (nativeDuelProcesses.Count == 0)
        {
            return;
        }
        (string actorId, BattleNativeDuelState state) = nativeDuelProcesses.Single();
        ushort pulse = (ushort)(heldButtons & ~state.PreviousButtons);
        ushort pending = state.Phase == BattleNativeDuelPhase.Ready ? (ushort)(state.PendingPulse | pulse) : (ushort)0;
        nativeDuelProcesses[actorId] = state with { PreviousButtons = heldButtons, PendingPulse = pending };
    }

    private void RestoreNativeDuel(string actorId, byte crisis, BattleNativeDuelState state, ExpandedHostedEvent? hostedEvent)
    {
        BattleCombatant actor = GetActor(actorId);
        bool hosted = OwnsHostedDuelRestore(hostedEvent, actorId) || state is { GameplayFinished: true, Phase: BattleNativeDuelPhase.Finisher, WidgetClosed: false };
        NativeLimitProgress progress = RequireNativeDuelProgress();
        if ((progress.Options978!.Value & 2) != 0 && definition.NativeBattleUiRandom is null)
        {
            throw new InvalidDataException("Saved automatic Duel lacks its actual4A7080 battle UI random owner.");
        }
        if (Outcome != BattleOutcome.Active || !actor.IsAlive && !(hosted && state.Phase == BattleNativeDuelPhase.Finisher) || actor.NativeLimit is not { Command: 11 } rules || crisis == 0 || state.InitialTimer != NativeBattleDuelRules.Timer(rules, crisis) || state.InitialTimer == 0 || (state.RemainingTimer <= 0 && !(state.Selection1D28DEF is not null && state.Phase == BattleNativeDuelPhase.Finisher)) || state.RemainingTimer > state.InitialTimer || !Enum.IsDefined(state.Phase) || state.InputRing.IsDefault || state.InputRing.Length != 8 || state.InputCursor >= 8 || state.AutomaticCountdown > 120 || state.WidgetPhase is not (0 or 1 or 2 or 3 or 4 or 9 or 11 or 12 or 13) || state.PublicationLatch > 4 || state.WidgetRevealQ12 > 4096)
        {
            throw new InvalidDataException("Saved Duel process contradicts its native character, timer or raw input ring.");
        }
        _ = NativeBattleDuelRules.ActionId(rules, state.MoveSetId);
        if (state.TargetRequest is null || (hosted != state.Selection1D28DEF.HasValue || state.TargetRequest.Selection is not BattleTargetSelection.Explicit { Actors: [BattleEnemyActorReference target] } || FindTargetActor(target) is null))
        {
            throw new InvalidDataException("Saved Duel target disagrees with the battle roster.");
        }
        nativeDuelProcesses.Add(actorId, state);
        actor.IsBusy = true;
        NativeLimitCallback? expected = state.Phase switch { BattleNativeDuelPhase.Opening => NativeLimitCallback.DuelOpening, BattleNativeDuelPhase.AwaitingEffect => NativeLimitCallback.DuelEffect, BattleNativeDuelPhase.Finisher => NativeLimitCallback.DuelFinisher, _ => null };
        if (state.Selection1D28DEF is byte selection)
        {
            if (selection < 254)
            {
                _ = NativeBattleDuelRules.ActionId(rules, selection);
            }
            if (hosted)
            {
                return;
            }
                throw new InvalidDataException("Saved native Duel selection requires its graphical descriptor owner.");
        }
        else
        {
            RequireNativeLimitCallback(actorId, expected);
        }
    }

    private readonly Dictionary<string, BattleNativeDuelState> nativeDuelProcesses = new(StringComparer.Ordinal);

}
