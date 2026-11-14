using Memoria.Battle;

namespace Memoria.FF7.Runtime;

public enum Ff7BattleTurnAdvanceBoundary
{
    PlayerReady,
    QueuedExecution,
    TerminalResolution,
    BattleEnded,
    Intro,
    LimitSelection,
    ActiveAction,
    PrimaryPresentation,
    Stalled
}

public readonly record struct Ff7BattleTurnAdvanceResult(Ff7BattleTurnAdvanceBoundary Boundary, int AdvancedSteps = 0);

public sealed partial class Ff7BattleSession
{
    // Allow the slowest full Stop countdown (4552 samples) before the ordinary 4096-step readiness bound.
    public const int MaximumTurnAdvanceSteps = 12288;

    /// <summary>Advance the native clocks to a new decision or queued action without dispatching presentation.</summary>
    public Ff7BattleTurnAdvanceResult AdvanceTurn(int maxSteps = MaximumTurnAdvanceSteps)
    {
        if (maxSteps is < 1 or > MaximumTurnAdvanceSteps)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSteps));
        }
        if (CurrentTurnBoundary() is { } boundary)
        {
            return new(boundary);
        }
        if (AwaitingTerminalResolution())
        {
            return new(Ff7BattleTurnAdvanceBoundary.TerminalResolution);
        }

        ushort readyPlayers = ReadyPlayerMask();
        List<Action> guestReadinessPublications = [];
        Ff7BattleSession candidate = (Ff7BattleSession)MemberwiseClone();
        // The clock replaces queue and guest state and writes only the physical actor array in place.
        candidate.state = state with { Actors = (Ff7BattleActorState[])state.Actors.Clone() };
        return BoundedBattleAdvance.Run<Ff7BattleSession, Ff7BattleTurnAdvanceResult>(candidate, maxSteps,
            (next, step) =>
            {
                bool changed = next.AdvanceGauges(guestReadinessPublications);
                if (!next.BattleQueueEmpty)
                {
                    return new(Ff7BattleTurnAdvanceBoundary.QueuedExecution, step);
                }
                if (next.AwaitingTerminalResolution())
                {
                    return new(Ff7BattleTurnAdvanceBoundary.TerminalResolution, step);
                }
                if ((next.ReadyPlayerMask() & ~readyPlayers) != 0)
                {
                    return new(Ff7BattleTurnAdvanceBoundary.PlayerReady, step);
                }
                return changed ? null : new(Ff7BattleTurnAdvanceBoundary.Stalled);
            },
            (next, result) =>
            {
                if (result.AdvancedSteps > 0)
                {
                    state = next.state;
                    foreach (Action publish in guestReadinessPublications)
                    {
                        publish();
                    }
                }
            },
            $"No FFVII battle turn boundary was reached within {maxSteps} native time steps.");
    }

    private void RefillTurnBasedGauges()
    {
        if (CurrentTurnBoundary() is not null)
        {
            return;
        }
        for (int step = 0; step < MaximumTurnAdvanceSteps; step++)
        {
            if (!AdvanceGauges() || !BattleQueueEmpty || ReadyPlayerMask() != 0)
            {
                return;
            }
        }
    }

    private Ff7BattleTurnAdvanceBoundary? CurrentTurnBoundary()
    {
        if (Outcome != Ff7BattleOutcome.Fighting)
        {
            return Ff7BattleTurnAdvanceBoundary.BattleEnded;
        }
        if (IntroActive)
        {
            return Ff7BattleTurnAdvanceBoundary.Intro;
        }
        if (guestAttacks.OfType<IFf7BattleGuestProcess>().Any(process => process.ProcessActive))
        {
            return Ff7BattleTurnAdvanceBoundary.LimitSelection;
        }
        if (BeatRushSelecting)
        {
            return Ff7BattleTurnAdvanceBoundary.LimitSelection;
        }
        if (state.Action is not null)
        {
            return Ff7BattleTurnAdvanceBoundary.ActiveAction;
        }
        if (HasPrimaryPresentationCallbacks)
        {
            return Ff7BattleTurnAdvanceBoundary.PrimaryPresentation;
        }
        return BattleQueueEmpty ? null : Ff7BattleTurnAdvanceBoundary.QueuedExecution;
    }

    private bool BattleQueueEmpty => state.Queue.ReadyRead == state.Queue.ReadyWrite && !state.Queue.Slots.Any(entry => entry is not null);

    private bool AwaitingTerminalResolution() => !HasLivingEnemy() || !HasLivingParty();

    private ushort ReadyPlayerMask() => Mask(state.Actors.Where(actor => actor.Actor.Slot < 3 && actor.AwaitingTurn && CanAct(actor)));
}
