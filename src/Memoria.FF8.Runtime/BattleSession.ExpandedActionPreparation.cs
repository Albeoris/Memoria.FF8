namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void PrepareAndPublishExpandedAction(BattleActionPresentation command)
    {
        BattleSnapshot before = Capture();
        IBattleStatusEffectHandler[] customHandlers = handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler).ToArray();
        BattleSession candidate = Restore(before, customHandlers);
        ExpandedBattlePresentationProcess? process = expandedPresentationProcess;
        candidate.preparingExpandedHostedAction = process is not null;
        candidate.expandedPresentationHost = process is null ? expandedPresentationHost : null;
        candidate.expandedPartyPresentation = expandedPartyPresentation;
        candidate.nativeAffinityAction = nativeAffinityAction;
        try
        {
            candidate.CommitExpandedAction(command);
            candidate.PrepareQueuedExpandedMagicEffects();
            PublishPreparedExpandedActionCandidate(before, customHandlers, candidate, command, process);
        }
        catch
        {
            candidate.ReleaseExpandedActionPresentation();
            throw;
        }
    }

    private void PublishPreparedExpandedActionCandidate(BattleSnapshot before, IBattleStatusEffectHandler[] customHandlers, BattleSession candidate, BattleActionPresentation command, ExpandedBattlePresentationProcess? process)
    {
        PreparedHostedDispatch? admission = null;
        ExpandedHostedEvent? hostedEvent = null;
        if (process is not null && (candidate.HasStagedExpandedHostedOperations || command.ExpandedAiCallback is not null))
        {
            BattleModelRoster roster = process.Graphics.ActorModels.Roster;
            uint callback = UsesNativeOrdinaryCompletion(command) ? 0x47DE70u : 0x47DDA0u;
            candidate.StageExpandedHostedCompletion(roster, command, callback);
            // Routed ordinary counters can publish only after their source mirror and its actual completion are staged.
            candidate.FlushEncounterCounterCommands();
            candidate.ValidateExpandedHostedInvocations(roster);
            ExpandedHostedPrefix prefix = candidate.PrepareExpandedHostedPrefix(process);
            admission = prefix.Dispatch;
            candidate.BindExpandedHostedPrefix(prefix, roster);
            hostedEvent = new(command, candidate.ActiveAction!, callback, admission.Completion?.Request.Token);
        }
        BattleSnapshot prepared = candidate.CaptureState();
        BattleSession preflight = Restore(before, customHandlers);
        preflight.expandedPartyPresentation = expandedPartyPresentation;
        preflight.CopyCandidateState(candidate, prepared, preparedExpandedAction: true);
        preflight.ValidatePreparedExpandedPresentation(candidate);
        if (admission is not null)
        {
            process!.ValidatePreparedDispatch(admission);
        }
        CopyCandidateState(candidate, prepared, preparedExpandedAction: true);
        TakeExpandedActionPresentation(candidate);
        if (admission is not null)
        {
            expandedHostedEvent = hostedEvent;
            expandedHostedActionToken = admission.Action?.Invocation.Token;
            process!.CommitDispatch(admission);
        }
    }

    private void ValidatePreparedExpandedPresentation(BattleSession candidate)
    {
        if (candidate.expandedEffectCommand is { } command)
        {
            _ = GetActor(command.ActorId);
            _ = GetActor(command.TargetId);
        }
        if (candidate.expandedMagicPresentation is { } queue)
        {
            foreach (ExpandedMagicCastPresentation presentation in queue)
            {
                _ = GetActor(presentation.Cast.ActorId);
                _ = GetActor(presentation.Cast.TargetId);
                foreach (ExpandedBattleContact contact in presentation.Contacts)
                {
                    _ = GetActor(contact.RecipientId);
                }
            }
        }
        foreach (ExpandedBattleContact contact in candidate.expandedModelContacts)
        {
            _ = GetActor(contact.RecipientId);
        }
        if (candidate.expandedGuardianFormEntry is { } entry &&
            (!byId.ContainsKey(entry.ActorId) || GetGuardianForm(entry.ActorId)?.InvocationId != entry.InvocationId))
        {
            throw new InvalidDataException("Prepared expanded GF entrance lacks its published actor or invocation.");
        }
        if (candidate.expandedGuardianSummonTargets is { } targets)
        {
            foreach (BattleCombatant target in targets)
            {
                if (!byId.ContainsKey(target.Id) || !ReferenceEquals(candidate.GetActor(target.Id), target))
                {
                    throw new InvalidDataException("Prepared expanded summon has an invalid target owner.");
                }
            }
        }
        if (candidate.nativeGuardianBoost is { } boost)
        {
            boost.Validate();
            if (candidate.expandedEffect?.IsGuardianSummon != true || candidate.expandedGuardianSummonTargets is not { Length: > 0 } ||
                candidate.expandedEffectCommand is not { NativeAction: { Command: 254 } action } summon ||
                ActiveAction is not { Phase: BattleActionPhase.Recovery, NativeAction: { Command: 254 } active } ||
                active.ActionId != action.ActionId || boost.ActionId != action.ActionId || ActiveAction.ActorId != summon.ActorId ||
                !GetActor(summon.ActorId).IsPlayer || nativeBattleUiRandom is null)
            {
                throw new InvalidDataException("Prepared expanded Boost lacks its hosted GF action, targets or UI random owner.");
            }
        }
    }

    private void PrepareQueuedExpandedMagicEffects()
    {
        if (expandedMagicPresentation is null)
        {
            return;
        }
        foreach (ExpandedMagicCastPresentation presentation in expandedMagicPresentation.Where(cast => cast.UsesNativeEffect))
        {
            BattleActionPresentation cast = presentation.Cast;
            presentation.PreparedEffect = CreateExpandedEffect(GetActor(cast.ActorId), cast, cast.NativeAction!, cast.NativeSequence ?? 1, presentation.Contacts);
        }
    }

    private void TakeExpandedActionPresentation(BattleSession candidate)
    {
        expandedEffect = candidate.expandedEffect;
        expandedHostedOperations = candidate.expandedHostedOperations;
        expandedHostedNextOperation = candidate.expandedHostedNextOperation;
        expandedHostedCurrentOperation = candidate.expandedHostedCurrentOperation;
        expandedEffectCommand = candidate.expandedEffectCommand;
        expandedContactBridge = candidate.expandedContactBridge;
        expandedExpectedContacts = candidate.expandedExpectedContacts;
        expandedModelContacts = candidate.expandedModelContacts;
        expandedModelRoster = candidate.expandedModelRoster;
        expandedPresentedOccurrences.Clear();
        expandedPresentedOccurrences.UnionWith(candidate.expandedPresentedOccurrences);
        expandedEffectEnemyDraw = candidate.expandedEffectEnemyDraw;
        expandedMagicPresentation = candidate.expandedMagicPresentation;
        expandedMagicCompletionMessage = candidate.expandedMagicCompletionMessage;
        expandedGuardianFormEntry = candidate.expandedGuardianFormEntry;
        expandedGuardianSummonTargets = candidate.expandedGuardianSummonTargets?.Select(actor => GetActor(actor.Id)).ToArray();
        expandedGuardianBoostInputEnabled = candidate.expandedGuardianBoostInputEnabled;
        expandedGuardianFormSkipRequested = candidate.expandedGuardianFormSkipRequested;
        nativeAffinityAction = candidate.nativeAffinityAction;
        nativeGuardianBoost = candidate.nativeGuardianBoost;

        candidate.expandedEffect = null;
        candidate.expandedEffectCommand = null;
        candidate.expandedContactBridge = null;
        candidate.expandedExpectedContacts = 0;
        candidate.expandedModelContacts = [];
        candidate.expandedModelRoster = null;
        candidate.expandedPresentedOccurrences.Clear();
        candidate.expandedEffectEnemyDraw = null;
        candidate.expandedMagicPresentation = null;
        candidate.expandedGuardianFormEntry = null;
        candidate.expandedGuardianSummonTargets = null;
    }

    private void ReleaseExpandedActionPresentation()
    {
        ReleaseExpandedEffect();
        if (expandedMagicPresentation is { } queue)
        {
            foreach (ExpandedMagicCastPresentation presentation in queue)
            {
                presentation.PreparedEffect?.Dispose();
            }
            expandedMagicPresentation = null;
        }
        expandedGuardianFormEntry?.Effect.Dispose();
        expandedGuardianFormEntry = null;
        expandedGuardianSummonTargets = null;
        expandedGuardianBoostInputEnabled = false;
        expandedEffectEnemyDraw = null;
    }
}
