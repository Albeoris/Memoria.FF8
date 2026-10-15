using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Transient audio follows committed battle events without becoming saved simulation state.</summary>
internal sealed class BattleAudioPresentation(Action stop, Action<int> playMusic, Action<int> playImpact)
{
    private BattleSession? battle;
    private int consumedEvents;
    private BattleOutcome? outcome;

    public bool Update(BattleSession? active)
    {
        bool ended = battle is not null && active is null;
        if (!ReferenceEquals(battle, active))
        {
            stop();
            battle = active;
            consumedEvents = active?.Events.Count ?? 0;
            outcome = null;
        }
        if (active is null)
        {
            return ended;
        }
        for (int index = consumedEvents; index < active.Events.Count; index++)
        {
            BattleEvent entry = active.Events[index];
            if (entry.Kind == "Damage" && entry.ActorId is not null)
            {
                BattleCombatant actor = active.Combatants.Single(actor => actor.Id == entry.ActorId);
                // Native47D970→502670 retains scene.out's enemy identity;507120 maps identity+150 to C0M(model).DAT.
                int actorId = actor.IsPlayer ? actor.ModelId : checked(actor.ModelId + 16);
                if (actor.IsPlayer ? actor.ModelId < 11 : actorId < 160)
                {
                    playImpact(actorId);
                }
            }
        }
        consumedEvents = active.Events.Count;
        if (outcome != active.Outcome)
        {
            // TODO(TD-009): Default Battle/Win/Lose tracks are presentation selection until native encounter music/flag routing is recovered
            int track = active.Outcome switch { BattleOutcome.Active => 5, BattleOutcome.Victory => 1, BattleOutcome.Defeat => 0, _ => throw new InvalidOperationException("Unknown battle outcome.") };
            playMusic(track);
            outcome = active.Outcome;
        }
        return ended;
    }
}
