using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private bool observingBattle;

    private static BattleSession CreateLiveBattleSession(BattleDefinition definition, BattleAtbMode mode)
    {
        return new BattleSession(definition, mode);
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
                ReplayBattleOutcome result = CaptureAutomaticBattleOutcome(occurrence);
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
        ReplayScriptedBattleOwner? owner = scripted is null ? null : new(scripted.FieldId, scripted.ObjectId, scripted.ScriptId, scripted.Operation, scripted.Flags, scripted.ProgramHash);
        ReplayBattleEncounter occurrence = new(Guid.NewGuid().ToString("N"), formation, BattleResources.FormationSourceHash(formation), scene, field, frame, source, owner);
        session.SetPendingBattleEncounter(occurrence);
    }

    private void CompleteRecordedBattle(BattleSession active)
    {
        ReplayBattleEncounter occurrence = session.State.PendingBattleEncounter ?? throw new InvalidOperationException("A live battle has no owned encounter occurrence.");
        ReplayBattleOutcome result = CaptureBattleOutcome(active, occurrence);
        if (occurrence.ScriptedOwner is not null)
        {
            Events.ReturnFromInteractiveBattle(checked((int)active.Frame), () => ApplyBattleResult(occurrence, result));
        }
        else
        {
            ApplyBattleResult(occurrence, result);
            RandomEncounterSessionState encounters = session.State.RandomEncounters ?? throw new InvalidOperationException("Random outcome lost its playthrough owner.");
            session.SetRandomEncounters(encounters with { Pending = null });
        }
    }
}
