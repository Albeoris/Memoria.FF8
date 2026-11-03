using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record EncounterNativeEventSelection(byte Ability, int? SetActorVariable = null);

/// <summary>Native message-bearing action branches, separated from ordinary attack decisions.</summary>
public static class EncounterNativeEvents
{
    public static EncounterNativeEventSelection? Select(string encounter, bool openingUnused, bool partyLineUnused, bool partyCountAtMostTwo, bool actorZeroBelowQuarterHp)
    {
        if (encounter != EncounterBindingResolver.Disc1Edea)
        {
            throw new InvalidDataException($"Encounter '{encounter}' has no supported native event adapter.");
        }
        if (openingUnused)
        {
            return new(8, 220);
        }
        if (partyLineUnused && partyCountAtMostTwo)
        {
            return new(9, 221);
        }
        if (actorZeroBelowQuarterHp && partyCountAtMostTwo)
        {
            return new(10);
        }
        return null;
    }
}

public sealed partial class BattleSession
{
    private ImmutableDictionary<string, ulong> encounterNativeEventTurns = ImmutableDictionary<string, ulong>.Empty;

    public ImmutableDictionary<string, ulong> CaptureEncounterNativeEvents() => encounterNativeEventTurns;

    public void RestoreEncounterNativeEvents(ImmutableDictionary<string, ulong> turns)
    {
        ArgumentNullException.ThrowIfNull(turns);
        if (turns.Any(pair => !byId.ContainsKey(pair.Key) || pair.Value == 0))
        {
            throw new InvalidDataException("Native encounter event snapshot requires allocated actor identities and positive accepted own turns.");
        }
        encounterNativeEventTurns = turns;
    }

    /// <summary>Queues at most one native event in the admitted main action, before its spell packet.</summary>
    private void QueueEncounterNativeEvents(BattleCombatant actor, ulong ownTurn)
    {
        if (definition.EncounterMod is not { } mod || actor.Id != mod.OriginalActorId)
        {
            return;
        }
        EncounterCompiledEventGate[] gates = mod.EventGates.Values.Where(gate => gate.Event == "nativeTaunts" && ResolveEncounterRole(gate.Actor) == actor.Id).ToArray();
        if (gates.Length == 0 || gates.Any(gate => ownTurn < gate.MinimumOwnTurn) || encounterNativeEventTurns.TryGetValue(actor.Id, out ulong lastTurn) && lastTurn >= ownTurn)
        {
            return;
        }
        if (gates.Length != 1)
        {
            throw new InvalidDataException("An original actor requires exactly one composed native taunt gate.");
        }
        bool openingUnused = EvaluateNativeAiCondition(actor, 220, 200, 0, 0);
        bool partyLineUnused = EvaluateNativeAiCondition(actor, 221, 200, 0, 0);
        bool partyCountAtMostTwo = EvaluateNativeAiCondition(actor, 6, 200, 4, 2);
        // Preserve native short-circuiting: selector0 is not read while an earlier branch wins.
        bool lowHp = !openingUnused && !(partyLineUnused && partyCountAtMostTwo) && EvaluateNativeAiCondition(actor, 0, 0, 1, 10);
        EncounterNativeEventSelection? selected = EncounterNativeEvents.Select(mod.Encounter, openingUnused, partyLineUnused, partyCountAtMostTwo, lowHp);
        if (selected is not null)
        {
            NativeBattleAiDefinition ai = actor.NativeAi ?? throw new InvalidDataException("Native encounter events require the original actor's guarded AI ability bank.");
            NativeBattleAiAbility ability = ai.Abilities[selected.Ability];
            QueuePreparedNativeAiCommand(actor, ability.Command, ability.Id, ability.Sequence, ResolveNativeAiTargetMask(actor, 200));
            if (selected.SetActorVariable is int variable)
            {
                WriteNativeActorVariable(actor, variable, 1, false);
            }
        }
        encounterNativeEventTurns = encounterNativeEventTurns.SetItem(actor.Id, ownTurn);
    }
}
