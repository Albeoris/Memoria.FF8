using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class AutoPlayParticipantPolicyException(NativePartyActionRequest request) : Exception("Automation deferred an action outside the actual participant's declared script group.")
{
    public NativePartyActionRequest Request { get; } = request;
}

internal sealed partial class AutoPlayController
{
    private void ObserveParticipantAction(NativePartyActionRequest request)
    {
        if (request.PhysicalCodeObjectId != request.ReceiverObjectId)
        {
            throw new AutoPlayParticipantPolicyException(request);
        }
    }

    private void RefreshParticipantPolicies(GameWorld world)
    {
        foreach (AutoPlayVisitCoverage visit in run.Coverage.Visits)
        {
            planner.RefreshParticipantPolicy(visit.Visit.Key, attempt => world.ReadAutoPlayParticipantPolicyFingerprint(attempt.Guard) == attempt.Guard.Fingerprint);
        }
    }

    private void RefreshParticipantChoice(AutoPlayChoiceCoverage choice)
    {
        if (choice.ActiveOption is not null || run.ActiveVisit is not string visit || run.ActiveEvent is not string eventId || run.ActiveTargetId is not string target)
        {
            return;
        }
        AutoPlayEventCoverage item = run.Coverage.Visits.Single(candidate => candidate.Visit.Key == visit).Events.Single(candidate => candidate.Event.Id == eventId);
        if (choice.PolicyScope is null && choice.PolicyDeferredOptions.Count == 0 && !item.ParticipantPolicyDeferrals.Any(attempt => attempt.ChoiceKey == choice.Key))
        {
            return;
        }
        GameWorld world = getWorld();
        AutoPlayChoiceSelection[] prefix = run.ActiveSelections?.ToArray() ?? throw new NotSupportedException("Participant-policy choice exploration requires its authentic ordered selection prefix.");
        string? persistent = run.VisitGuards.TryGetValue(visit, out AutoPlayVisitGuard? guard) && guard.Version == AutoPlayVisitGuard.CurrentVersion ? world.Events.ReadPersistentPredicateFingerprint(guard.Reads) : null;
        var scope = new { visit, eventId, target, run.ActiveConditionSignature, persistent, Bindings = Enumerable.Range(0, 3).Select(slot => world.Events.Scene.PartyBindingMemory?.At(slot)).ToArray(), Retained = world.Events.Scene.NativeActorObjectIds, Characters = world.Events.Scene.NativeActorObjectIds.Select(id => world.Events.Actors[id].CharacterId).ToArray(), prefix };
        string scopeKey = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(scope)));
        int[] deferred = item.ParticipantPolicyDeferrals.Where(attempt => attempt.TargetId == target && attempt.SourceConditionSignature == run.ActiveConditionSignature && attempt.ChoiceKey == choice.Key && attempt.Selections.Count == prefix.Length + 1 && attempt.Selections.Take(prefix.Length).SequenceEqual(prefix) && world.ReadAutoPlayParticipantPolicyFingerprint(attempt.Guard) == attempt.Guard.Fingerprint).Select(attempt => attempt.Selections[^1].Selection).Distinct().ToArray();
        planner.RefreshParticipantChoice(choice.Key, scopeKey, deferred);
    }

    private void DeferParticipantAction(NativePartyActionRequest request)
    {
        if (run.ActiveVisit is not string visit || run.ActiveEvent is not string eventId || run.ActiveTargetId is not string target || run.ActiveBeforeCheckpoint is not string beforeEvent || run.ActiveConditionSignature is not string signature || run.ActiveSelections is null || request.FieldId != run.ActiveField || request.CallerObjectId != run.ActiveObjectId || request.PhysicalCodeObjectId == request.ReceiverObjectId || !run.VisitGuards.TryGetValue(visit, out AutoPlayVisitGuard? sourceGuard))
        {
            throw new NotSupportedException("The reached foreign participant action has no exact active interaction, original source guard or authentic pre-event checkpoint.");
        }
        AutoPlayChoiceCoverage? choice = null;
        if (run.SceneChoices.Count > 0)
        {
            string key = run.SceneChoices[^1];
            choice = run.Coverage.Choices.Single(item => item.Key == key);
            if (choice.ActiveOption is not int active || choice.BeforeCheckpoint is null || choice.BeforeSelections is null || run.ActiveSelections.Count != choice.BeforeSelections.Count + 1 || !run.ActiveSelections.Take(choice.BeforeSelections.Count).SequenceEqual(choice.BeforeSelections) || run.ActiveSelections[^1] != new AutoPlayChoiceSelection(key, active))
            {
                throw new NotSupportedException("The reached party action lacks its exact selected option and authentic pre-choice prefix.");
            }
        }
        else if (run.ActiveSelections.Count != 0)
        {
            throw new NotSupportedException("A repeated or legacy choice path cannot be suppressed as a whole participant action.");
        }
        AutoPlayChoiceSelection[] selections = run.ActiveSelections.ToArray();
        ScriptedInputMetadata[] selectedInputs = selections.Where(selection => run.ScriptedChoiceInputs.ContainsKey(selection.Key)).Select(selection => run.ScriptedChoiceInputs[selection.Key]).ToArray();
        GameState diagnostic = CaptureState();
        run.ParticipantPolicyDiagnosticOnly = true;
        SaveBundle($"participant-policy-before-request-{run.Steps:D10}", diagnostic);
        run.ParticipantPolicyDiagnosticOnly = false;
        string checkpoint = choice?.BeforeCheckpoint ?? beforeEvent;
        restore(GameSession.Load(Path.Combine(runDirectory!, checkpoint)));
        GameWorld source = getWorld();
        source.EnsureField();
        AutoPlayParticipantPolicyGuard guard;
        try
        {
            guard = source.ReadAutoPlayParticipantPolicyGuard(request, eventId, selectedInputs, sourceGuard);
            if (source.Events.Scene.PartyBindingMemory!.At(request.PartySlot) != request.PhysicalBinding || source.Events.Scene.NativeActorObjectIds[request.PhysicalBinding] != request.ReceiverObjectId || source.Events.Actors[request.ReceiverObjectId].CharacterId != request.ReceiverCharacterId)
            {
                throw new NotSupportedException("The actual participant binding changed between the reached request and its authentic pre-choice checkpoint.");
            }
            restore(GameSession.Load(Path.Combine(runDirectory!, beforeEvent)));
            GameWorld entry = getWorld();
            entry.EnsureField();
            if (entry.ReadAutoPlayParticipantPolicyFingerprint(guard) != guard.Fingerprint || entry.ReadAutoPlayConditions(eventId, run.ActiveObjectId).Fingerprint != signature)
            {
                throw new NotSupportedException("Participant-policy inputs changed before the choice; the source event cannot be suppressed under its entry guards.");
            }
            restore(GameSession.Load(Path.Combine(runDirectory!, checkpoint)));
            getWorld().EnsureField();
        }
        catch
        {
            // Diagnostic callbacks are never resumed; retain the authentic pre-event owner on refusal.
            restore(GameSession.Load(Path.Combine(runDirectory!, beforeEvent)));
            throw;
        }
        bool added = planner.DeferParticipantAction(visit, eventId, new(target, signature, guard, selections, request, choice?.Key, choice?.FirstOption ?? 0, choice?.OptionCount ?? 0));
        if (added)
        {
            run.LastProgressStep = run.Steps;
            ResetIdleDeadline();
        }
        run.ActiveObservedInput = null;
        run.ObserveContactInput = false;
        run.ObservedControlLoss = false;
        run.SettleFrames = 0;
        run.SceneFrames = 0;
        run.ScriptedInputSteps = 0;
        if (choice is not null)
        {
            choice.ActiveOption = null;
            run.SceneChoices.Remove(choice.Key);
            run.ActiveSelections = choice.BeforeSelections!.ToList();
            RefreshParticipantChoice(choice);
            // Exhaustion is committed separately, so it cannot be mistaken for a control return.
            choice.PolicyExhausted = false;
            AutoPlayChoiceStep next = planner.PlanChoice(choice.Key);
            if (next.Selection is not null)
            {
                run.ParticipantPolicyResumeChoice = choice.Key;
                SaveBundle($"participant-policy-choice-return-{run.Steps:D10}", CaptureState());
                Status = "Participant action deferred; restored the authentic pre-choice checkpoint for another native alternative.";
                return;
            }
            if (run.SceneChoices.Count != 0)
            {
                throw new NotSupportedException("All alternatives of a nested participant-policy prefix are deferred; its enclosing option is not automatically completed or discarded.");
            }
            if (next.Kind == AutoPlayChoiceStepKind.RestoreContinuation)
            {
                planner.FinishChoice(choice.Key);
                if (run.PrimaryContinuation is null || !FinishChoices())
                {
                    throw new NotSupportedException("A completed alternative has no authenticated canonical continuation.");
                }
                SaveBundle($"participant-policy-canonical-return-{run.Steps:D10}", CaptureState());
                return;
            }
            planner.FinishPolicyDeferredChoice(choice.Key);
        }
        restore(GameSession.Load(Path.Combine(runDirectory!, beforeEvent)));
        getWorld().EnsureField();
        ClearParticipantPolicyAttempt();
        RefreshParticipantPolicies(getWorld());
        SaveBundle($"participant-policy-event-return-{run.Steps:D10}", CaptureState());
        Status = "Participant actions are policy-deferred under their observed guards; restored the authentic pre-event checkpoint without completion credit.";
    }

    private void ResumeParticipantChoice(string key)
    {
        AutoPlayChoiceCoverage choice = run.Coverage.Choices.Single(item => item.Key == key);
        RefreshParticipantChoice(choice);
        AutoPlayChoiceStep next = planner.PlanChoice(key);
        int selection = next.Selection ?? throw new NotSupportedException("The restored participant-policy choice no longer has its guarded next alternative.");
        planner.BeginChoiceOption(key, selection);
        run.ParticipantPolicyResumeChoice = null;
        run.SceneChoices.Add(key);
        run.ActiveSelections?.Add(new(key, selection));
        if (key.StartsWith("input:", StringComparison.Ordinal))
        {
            ScriptedInputMetadata input = RestoreScriptedChoiceInput(choice);
            PulseScriptedInput(input.Masks[selection], input, getWorld().Events.IsUserControlEnabled, choice.InputCheckpointPrepared);
        }
        else
        {
            run.DialogueSelections[key] = selection;
            ApplyDialogueSelection(selection);
        }
    }

    private void ClearParticipantPolicyAttempt()
    {
        run.ActiveVisit = null;
        run.ActiveEvent = null;
        run.ActiveStory = null;
        run.ActiveObjectId = -1;
        run.ActiveConditionSignature = null;
        run.ActiveSelections = [];
        run.ActiveTravelRoute = null;
        run.ActiveBeforeCheckpoint = null;
        run.ActiveTargetId = null;
        run.ActiveAbsenceGuardKey = null;
        run.ActiveAvailabilityFingerprint = null;
        run.ActiveObservedInput = null;
        run.ObserveContactInput = false;
        run.ObservedControlLoss = false;
        run.ParticipantPolicyResumeChoice = null;
        run.PrimaryContinuation = null;
        run.PrimarySelections = null;
    }
}
