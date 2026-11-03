using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record EncounterNativeEventState(ulong LastOwnTurn = 0, bool OpeningPending = true, bool PartyPending = false, int OpeningCursor = 0, int PartyCursor = 0)
{
    public (EncounterNativeEventState State, EncounterNativeTextCue? Line) Admit(ulong ownTurn, ulong minimumTurn, EncounterNativeEventResources resources)
    {
        if (ownTurn < minimumTurn || LastOwnTurn >= ownTurn)
        {
            return (this, null);
        }
        if (OpeningPending)
        {
            int next = OpeningCursor + 1;
            return (this with { LastOwnTurn = ownTurn, OpeningCursor = next, OpeningPending = next < resources.Opening.Length }, resources.Opening[OpeningCursor]);
        }
        if (PartyPending)
        {
            int next = PartyCursor + 1;
            return (this with { LastOwnTurn = ownTurn, PartyCursor = next, PartyPending = next < resources.Party.Length }, resources.Party[PartyCursor]);
        }
        return (this with { LastOwnTurn = ownTurn }, null);
    }
}

public sealed partial class BattleSession
{
    private ImmutableDictionary<string, EncounterNativeEventState> encounterNativeEventTurns = ImmutableDictionary<string, EncounterNativeEventState>.Empty;
    private EncounterNativeEventResources? encounterNativeEventResources;

    public ImmutableDictionary<string, EncounterNativeEventState> CaptureEncounterNativeEvents() => encounterNativeEventTurns;

    public void RestoreEncounterNativeEvents(ImmutableDictionary<string, EncounterNativeEventState> turns)
    {
        ArgumentNullException.ThrowIfNull(turns);
        if (turns.Any(pair => !byId.ContainsKey(pair.Key) || pair.Value is null || pair.Value.OpeningCursor is < 0 or > 1 || pair.Value.PartyCursor is < 0 or > 2 || pair.Value.OpeningPending && pair.Value.OpeningCursor != 0 || pair.Value.PartyPending && pair.Value.PartyCursor >= 2))
        {
            throw new InvalidDataException("Native encounter event snapshot requires allocated actor identities and valid deferred native text cursors.");
        }
        encounterNativeEventTurns = turns;
        encounterNativeEventResources = null;
    }

    private EncounterNativeEventResources RequireEncounterNativeEventResources(BattleCombatant actor)
    {
        return encounterNativeEventResources ??= definition.EncounterMod?.NativeEvents ?? throw new InvalidDataException($"Native encounter events for actor '{actor.Id}' require their selected-resource binding in the captured encounter definition.");
    }

    private EncounterCompiledEventGate? EncounterNativeGate(BattleCombatant actor)
    {
        if (definition.EncounterMod is not { } mod || mod.Encounter != EncounterBindingResolver.Disc1Edea || actor.Id != mod.OriginalActorId || EncounterPolicyOf(actor) is null)
        {
            return null;
        }
        EncounterCompiledEventGate[] gates = mod.EventGates.Values.Where(gate => gate.Event == "nativeTaunts" && ResolveEncounterRole(gate.Actor) == actor.Id).ToArray();
        if (gates.Length > 1)
        {
            throw new InvalidDataException("An original actor requires exactly one composed native taunt gate.");
        }
        return gates.SingleOrDefault() ?? new("original", "nativeTaunts", 1);
    }

    /// <summary>Native terminal eligibility precedes normal policy admission and its silence checks.</summary>
    private bool TryRunEncounterNativeTerminal(BattleCombatant actor, int callback)
    {
        if (callback != 1 || !actor.IsActive || !actor.IsAlive || EncounterNativeGate(actor) is null || (NativeSavedStatusBits(actor) & 0x20) != 0)
        {
            return false;
        }
        if (encounterPolicies is { TerminalStarted: true })
        {
            return true;
        }
        _ = RequireEncounterNativeEventResources(actor);
        bool openingUnused = EvaluateNativeAiCondition(actor, 220, 200, 0, 0);
        if (openingUnused)
        {
            return false;
        }
        bool partyCountAtMostTwo = EvaluateNativeAiCondition(actor, 6, 200, 4, 2);
        if (EvaluateNativeAiCondition(actor, 221, 200, 0, 0) && partyCountAtMostTwo)
        {
            WriteNativeActorVariable(actor, 221, 1, false);
            EncounterNativeEventState pending = encounterNativeEventTurns.GetValueOrDefault(actor.Id) ?? new();
            encounterNativeEventTurns = encounterNativeEventTurns.SetItem(actor.Id, pending with { PartyPending = true });
            return false;
        }
        if (!EvaluateNativeAiCondition(actor, 0, 0, 1, 10) || !partyCountAtMostTwo)
        {
            return false;
        }
        byte command = 0;
        ushort action = 0;
        byte sequence = 0;
        PrepareNativeAiAbility(actor, 10, ref command, ref action, ref sequence);
        QueuePreparedNativeAiCommand(actor, command, action, sequence, ResolveNativeAiTargetMask(actor, 200));
        EncounterCompiledDefinition mod = definition.EncounterMod!;
        encounterPolicies ??= new(mod.GenerationIdentity, ImmutableDictionary<string, EncounterActionCursor>.Empty);
        encounterPolicies = encounterPolicies with { TerminalStarted = true };
        CleanupEncounterPoliciesAtTerminal();
        return true;
    }

    /// <summary>Marks first native admission independently of the delayed, one-line-per-turn taunt owner.</summary>
    private void QueueEncounterNativeEvents(BattleCombatant actor, ulong ownTurn)
    {
        EncounterCompiledEventGate? gate = EncounterNativeGate(actor);
        if (gate is null)
        {
            return;
        }
        EncounterNativeEventResources resources = RequireEncounterNativeEventResources(actor);
        EncounterNativeEventState state = encounterNativeEventTurns.GetValueOrDefault(actor.Id) ?? new();
        if (EvaluateNativeAiCondition(actor, 220, 200, 0, 0))
        {
            WriteNativeActorVariable(actor, 220, 1, false);
        }
        (state, EncounterNativeTextCue? line) = state.Admit(ownTurn, gate.MinimumOwnTurn, resources);
        if (line is not null)
        {
            NativeBattleGraphicsResource resource = FindNativeOriginalGraphicsBinding(actor.Id)!.Resource;
            BattleEvent message = new(Frame, "NativeText", actor.Id, null, line.Duration, resource.ReadText91(line.Text)) { NativeMessageMode = 3, NativeTextIdentity = BattleTextIdentity.Enemy(actor.ModelId, line.Text) };
            StopTurnAdvance();
            QueueNativeDescriptorMessage(message, 0);
        }
        encounterNativeEventTurns = encounterNativeEventTurns.SetItem(actor.Id, state);
    }
}
