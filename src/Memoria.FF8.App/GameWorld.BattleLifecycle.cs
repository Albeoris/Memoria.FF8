using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private bool observingBattle;

    internal static BattleSession CreateLiveBattleSession(BattleDefinition definition, BattleAtbMode mode)
    {
        BattleSession active = new(definition, mode);
        if (definition.NativeGraphics is null && definition.NativeSupportRules is not null)
        {
            active.CompleteComponentBattleEntry();
        }
        return active;
    }

    private void ObservePendingBattle()
    {
        if (observingBattle)
        {
            return;
        }
        observingBattle = true;
        try
        {
            ObserveBattleOccurrence();
            if (Events.PendingHostedBattle is { Backend: NativeBattleBackend.AutomaticVictory } && session.State.PendingBattleEncounter is { } occurrence)
            {
                BattleResult result = CaptureAutomaticBattleOutcome(occurrence);
                Events.ReturnFromHostedBattle(0, () => ApplyBattleResult(occurrence, result));
            }
        }
        finally
        {
            observingBattle = false;
        }
    }

    private void ObserveBattleOccurrence()
    {
        if (session.State.PendingBattleEncounter is not null || battle?.Outcome == BattleOutcome.Defeat)
        {
            return;
        }
        NativeBattleCompletion? scripted = Events.PendingHostedBattle;
        RandomEncounterRequest? random = session.State.RandomEncounters?.Pending;
        if (scripted is null && random is null)
        {
            return;
        }
        int formation = scripted?.Encounter ?? random!.Formation;
        SceneKind scene = scripted is null ? random!.Scene : SceneKind.Field;
        int field = scripted?.FieldId ?? random!.FieldId;
        long frame = scripted?.Frame ?? random!.Frame;
        string source = scripted?.ProgramHash ?? random!.SourceHash;
        ScriptedBattleOwner? owner = scripted is null ? null : new(scripted.FieldId, scripted.ObjectId, scripted.ScriptId, scripted.Operation, scripted.Flags, scripted.ProgramHash);
        BattleEncounterOccurrence occurrence = new(Guid.NewGuid().ToString("N"), formation, BattleResources.FormationSourceHash(formation), scene, field, frame, source, owner);
        session.SetPendingBattleEncounter(occurrence);
    }

    private void CompleteBattle(BattleSession active)
    {
        BattleEncounterOccurrence occurrence = session.State.PendingBattleEncounter ?? throw new InvalidOperationException("A live battle has no owned encounter occurrence.");
        BattleResult result = CaptureBattleOutcome(active, occurrence);
        session.SetBattle(active.Capture());
        if (occurrence.ScriptedOwner is not null)
        {
            Events.ReturnFromInteractiveBattle(checked((int)active.Frame), () => ApplyBattleResult(occurrence, result), active.NativeResult!.Value);
        }
        else
        {
            ApplyBattleResult(occurrence, result);
            RandomEncounterSessionState encounters = session.State.RandomEncounters ?? throw new InvalidOperationException("Random outcome lost its playthrough owner.");
            session.SetRandomEncounters(encounters with { Pending = null });
        }
    }
}
