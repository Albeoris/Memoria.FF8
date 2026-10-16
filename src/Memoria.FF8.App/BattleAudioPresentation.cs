using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Transient audio follows committed battle events without becoming saved simulation state.</summary>
internal sealed class BattleAudioPresentation(Action stop, Action<int> playMusic, Action<int> playImpact, Action<BattleCombatant, BattleSequenceSoundCue>? playSequence = null)
{
    private BattleSession? battle;
    private int consumedEvents;
    private BattleOutcome? outcome;
    private readonly HashSet<(string Actor, long Frame, int Index)> sequenceSounds = [];
    private bool sequenceInitialized;

    public bool Update(BattleSession? active, IReadOnlyList<BattleSequenceSoundCue>? sounds = null)
    {
        bool ended = battle is not null && active is null;
        bool changed = !ReferenceEquals(battle, active);
        if (changed)
        {
            stop();
            battle = active;
            consumedEvents = active?.Events.Count ?? 0;
            outcome = null;
            sequenceSounds.Clear();
            sequenceInitialized = false;
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
                if (actor.ModelId >= 0 && (actor.IsPlayer ? actor.ModelId < 11 : actorId < 160))
                {
                    playImpact(actorId);
                }
            }
        }
        consumedEvents = active.Events.Count;
        bool suppressSequenceHistory = !sequenceInitialized;
        foreach (BattleSequenceSoundCue cue in sounds ?? [])
        {
            if (sequenceSounds.Add((cue.ActorId, cue.ActionFrame, cue.CueIndex)) && !suppressSequenceHistory)
            {
                BattleCombatant actor = active.Combatants.Single(actor => actor.Id == cue.ActorId);
                playSequence?.Invoke(actor, cue);
            }
        }
        if (sounds is not null)
        {
            sequenceInitialized = true;
        }
        if (outcome != active.Outcome)
        {
            // TODO(TD-009): Default Battle/Win/Lose tracks are presentation selection until native encounter music/flag routing is recovered
            int track = active.Outcome switch { BattleOutcome.Active or BattleOutcome.ScriptedExit => 5, BattleOutcome.Victory => 1, BattleOutcome.Defeat => 0, _ => throw new InvalidOperationException("Unknown battle outcome.") };
            playMusic(track);
            outcome = active.Outcome;
        }
        return ended;
    }
}
