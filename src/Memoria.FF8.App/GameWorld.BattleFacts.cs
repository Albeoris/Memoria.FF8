using System.Security.Cryptography;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class BattleFactReplayDiagnostics
{
    public int LiveBattleSessionsCreated { get; internal set; }
    public int RandomEncounterChecks { get; internal set; }
    public int EncounterMovementRequests { get; internal set; }
    public int EncountersApplied { get; internal set; }
    public int OutcomesApplied { get; internal set; }
}

internal sealed partial class GameWorld
{
    private bool IsCausalReconstruction { get; set; }
    private BattleFactReplayDiagnostics? BattleFactDiagnostics { get; set; }

    private BattleSession CreateLiveBattleSession(BattleDefinition definition, BattleAtbMode mode)
    {
        if (IsCausalReconstruction)
        {
            throw new InvalidOperationException("Factual reconstruction cannot create or simulate a live battle.");
        }
        if (BattleFactDiagnostics is not null)
        {
            BattleFactDiagnostics.LiveBattleSessionsCreated++;
        }
        return new BattleSession(definition, mode);
    }

    private void ObservePendingBattle()
    {
        ObserveBattleOccurrence(session.ReplayJournal);
        if (!IsCausalReconstruction && Events.PendingHostedBattle is { Backend: NativeBattleBackend.AutomaticVictory } && session.State.PendingBattleEncounter is { } occurrence)
        {
            ReplayBattleOutcome result = CaptureAutomaticBattleOutcome(occurrence);
            ReplayJournalEntry entry = new(ReplayEntryKind.BattleOutcome, session.State.Scene, session.State.FieldId, Events.Frame) { BattleOutcome = result, ResultScene = session.State.Scene, ResultFieldId = session.State.FieldId, ResultFrame = Events.Frame };
            Events.ReturnFromHostedBattle(0, () => ApplyBattleResult(occurrence, result), entry);
        }
    }

    private void ObserveBattleOccurrence(ReplayJournal? journal)
    {
        if (IsCausalReconstruction || session.State.PendingBattleEncounter is not null || battle?.Outcome == BattleOutcome.Defeat)
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
        string occurrenceId = (journal?.SessionId ?? "live-" + Guid.NewGuid().ToString("N")) + ":battle:" + (session.State.AppliedBattleResultIds.Length + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        ReplayBattleEncounter fact = new(occurrenceId, formation, BattleResources.FormationSourceHash(formation), scene, field, frame, source, owner);
        session.SetPendingBattleEncounter(fact);
        session.RecordReplayEntry(new(ReplayEntryKind.BattleEncounter, scene, field, frame) { BattleEncounter = fact, ResultScene = session.State.Scene, ResultFieldId = session.State.FieldId, ResultFrame = frame, ResultTrigger = (int)PlayerTriggerKind.Battle });
        if (session.ReplayJournal is { IsComplete: true })
        {
            session.CompactEncounterJournal();
        }
    }

    private void CompleteRecordedBattle(BattleSession active)
    {
        ReplayBattleEncounter occurrence = session.State.PendingBattleEncounter ?? throw new InvalidOperationException("A live battle has no recorded encounter occurrence.");
        ReplayBattleOutcome result = CaptureBattleOutcome(active, occurrence);
        long frame = session.State.Scene == SceneKind.WorldMap ? worldScene!.State.Frame : Events.Frame;
        ReplayJournalEntry entry = new(ReplayEntryKind.BattleOutcome, session.State.Scene, session.State.FieldId, frame) { BattleOutcome = result, ResultScene = session.State.Scene, ResultFieldId = session.State.FieldId, ResultFrame = frame };
        if (occurrence.ScriptedOwner is not null)
        {
            Events.ReturnFromInteractiveBattle(checked((int)active.Frame), () => ApplyBattleResult(occurrence, result), entry);
        }
        else
        {
            using (session.BeginReplayCapture())
            {
                ApplyBattleResult(occurrence, result);
                RandomEncounterSessionState encounters = session.State.RandomEncounters ?? throw new InvalidOperationException("Random outcome lost its playthrough owner.");
                session.SetRandomEncounters(encounters with { Pending = null });
            }
            session.RecordReplayEntry(entry);
        }
    }

    private void RecordCompletedDefeat(BattleSession active)
    {
        ReplayBattleEncounter occurrence = session.State.PendingBattleEncounter ?? throw new InvalidOperationException("A completed defeat has no encounter occurrence.");
        ReplayBattleOutcome result = CaptureBattleOutcome(active, occurrence);
        long frame = session.State.Scene == SceneKind.WorldMap ? worldScene!.State.Frame : Events.Frame;
        using (session.BeginReplayCapture())
        {
            ApplyBattleResult(occurrence, result);
            if (session.State.RandomEncounters is { Pending: not null } encounters)
            {
                session.SetRandomEncounters(encounters with { Pending = null });
            }
        }
        session.RecordReplayEntry(new(ReplayEntryKind.BattleOutcome, session.State.Scene, session.State.FieldId, frame) { BattleOutcome = result, ResultScene = session.State.Scene, ResultFieldId = session.State.FieldId, ResultFrame = frame });
    }

    private void ApplyRecordedBattleEncounter(ReplayBattleEncounter fact)
    {
        fact.Validate();
        EnsureField();
        if (session.State.PendingBattleEncounter is not null || session.State.Scene != fact.SourceScene || session.State.FieldId != fact.SourceFieldId || BattleResources.FormationSourceHash(fact.Formation) != fact.FormationHash)
        {
            throw new InvalidDataException("Encounter fact differs from its current scene or original formation resource.");
        }
        string source = fact.SourceScene == SceneKind.Field ? Convert.ToHexString(SHA256.HashData(Field!.Jsm)) : worldScene!.AllocationProgramSha256;
        if (source != fact.SourceHash)
        {
            throw new InvalidDataException("Encounter fact belongs to a different original scene resource.");
        }
        if (fact.ScriptedOwner is { } owner)
        {
            NativeBattleCompletion current = Events.PendingHostedBattle ?? throw new InvalidDataException("A scripted encounter fact requires its actual current BATTLE owner.");
            if (current.ObjectId != owner.ObjectId || current.ScriptId != owner.ScriptId || current.Operation != owner.Operation || current.ProgramHash != owner.ProgramHash || current.Flags != owner.Flags || current.Encounter != fact.Formation)
            {
                throw new InvalidDataException("Scripted encounter fact differs from the current native issuing operation.");
            }
        }
        session.SetPendingBattleEncounter(fact);
        session.RecordReplayEntry(new(ReplayEntryKind.BattleEncounter, session.State.Scene, session.State.FieldId, fact.SourceFrame) { BattleEncounter = fact, ResultScene = session.State.Scene, ResultFieldId = session.State.FieldId, ResultFrame = session.State.Scene == SceneKind.WorldMap ? worldScene!.State.Frame : Events.Frame, ResultTrigger = (int)PlayerTriggerKind.Battle });
        if (BattleFactDiagnostics is not null)
        {
            BattleFactDiagnostics.EncountersApplied++;
        }
    }

    private void ApplyRecordedBattleOutcome(ReplayBattleOutcome result)
    {
        ReplayBattleEncounter occurrence = session.State.PendingBattleEncounter ?? throw new InvalidDataException("Battle outcome has no preceding encounter occurrence.");
        long frame = session.State.Scene == SceneKind.WorldMap ? worldScene!.State.Frame : Events.Frame;
        ReplayJournalEntry entry = new(ReplayEntryKind.BattleOutcome, session.State.Scene, session.State.FieldId, frame) { BattleOutcome = result, ResultScene = session.State.Scene, ResultFieldId = session.State.FieldId, ResultFrame = frame };
        if (occurrence.ScriptedOwner is not null && result.Outcome != BattleOutcome.Defeat)
        {
            Events.ReturnFromHostedBattle(0, () => ApplyBattleResult(occurrence, result), entry);
        }
        else
        {
            using (session.BeginReplayCapture())
            {
                ApplyBattleResult(occurrence, result);
            }
            session.RecordReplayEntry(entry);
        }
        if (session.State.Scene == SceneKind.WorldMap && Field is not null)
        {
            session.SetEventState(Events.Capture());
        }
        if (BattleFactDiagnostics is not null)
        {
            BattleFactDiagnostics.OutcomesApplied++;
        }
    }
}
