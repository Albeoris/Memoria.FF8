namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleDuelWidgetFacts? ReadNativeDuelWidgetFacts()
    {
        if (nativeDuelProcesses.Count == 0)
        {
            return null;
        }
        (string actorId, BattleNativeDuelState state) = nativeDuelProcesses.Single();
        if (state.Phase == BattleNativeDuelPhase.Opening || state.WidgetClosed)
        {
            return null;
        }
        bool automatic = GetActor(actorId).IsAlive && (RequireNativeDuelProgress().Options978!.Value & 2) != 0;
        bool readsInput = state.WidgetPhase == 4 && automatic && state.AutomaticCountdown is 30 or 31 ||
            state.WidgetPhase is 3 or 4 && !automatic && state.PendingPulse != 0;
        return new(actorId, state, automatic, readsInput ? GetNativeDuelChoices(actorId) : []);
    }

    private void ApplyNativeDuelWidgetUpdate(NativeBattleDuelWidgetUpdated update)
    {
        NativeBattleDuelWidgetFacts facts = update.Facts;
        if (!nativeDuelProcesses.TryGetValue(facts.ActorId, out BattleNativeDuelState? current) || !ReferenceEquals(current, facts.State))
        {
            throw new InvalidDataException("Duel window6 updated a stale gameplay request.");
        }
        nativeDuelProcesses[facts.ActorId] = update.State;
        if (update.State is { WidgetClosed: true, GameplayFinished: true })
        {
            EndNativeDuel(facts.ActorId);
        }
    }
}
