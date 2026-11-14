namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void PrepareAndPublishExpandedAction(BattleActionPresentation command)
    {
        BattleSnapshot before = Capture();
        IBattleStatusEffectHandler[] customHandlers = handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler).ToArray();
        BattleSession candidate = Restore(before, customHandlers);
        candidate.expandedPresentationHost = expandedPresentationHost;
        candidate.nativeAffinityAction = nativeAffinityAction;
        try
        {
            candidate.CommitExpandedAction(command);
            candidate.PrepareQueuedExpandedMagicEffects();
            BattleSnapshot prepared = candidate.CaptureState();
            BattleSession preflight = Restore(before, customHandlers);
            preflight.CopyCandidateState(candidate, prepared, preparedExpandedAction: true);
            preflight.ValidatePreparedExpandedPresentation(candidate);
            CopyCandidateState(candidate, prepared, preparedExpandedAction: true);
            TakeExpandedActionPresentation(candidate);
        }
        catch
        {
            candidate.ReleaseExpandedActionPresentation();
            throw;
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
