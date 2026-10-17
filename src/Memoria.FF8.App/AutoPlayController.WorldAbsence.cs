using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class AutoPlayWorldAbsenceException(NativeWorldTransitionRequest request, AutoPlayWorldAbsenceReceipt receipt) : Exception("The actual native world packet has no allocated controlled foot object.")
{
    internal NativeWorldTransitionRequest Request { get; } = request;
    internal AutoPlayWorldAbsenceReceipt Receipt { get; } = receipt;
}

internal sealed partial class AutoPlayController
{
    private void ObserveWorldEntry(NativeWorldTransitionRequest request)
    {
        if (getWorld().InspectAutoPlayWorldAbsence(request) is { } receipt)
        {
            throw new AutoPlayWorldAbsenceException(request, receipt);
        }
    }

    private void DeferUnavailableWorldEntry(NativeWorldTransitionRequest request, AutoPlayWorldAbsenceReceipt receipt)
    {
        ValidateNativeBacktrack(run);
        string visit = run.ActiveVisit!;
        string eventId = run.ActiveEvent!;
        string signature = run.ActiveConditionSignature!;
        string checkpoint = run.ActiveBeforeCheckpoint!;
        if (request.SourceFieldId != run.ActiveField || request.SourceObjectId != run.ActiveObjectId || request.Origin != NativeWorldTransitionOrigin.Script || receipt.Version != 1 || unchecked((byte)request.Transition.Mode) != receipt.EntityType)
        {
            throw new NotSupportedException("The unavailable world request lacks its exact active native source and packet provenance.");
        }
        GameState diagnostic = CaptureState();
        SaveBundle($"world-unavailable-{run.Steps:D10}", diagnostic);
        GameState before = GameSession.Load(Path.Combine(runDirectory!, checkpoint));
        try
        {
            restore(before);
            GameWorld source = getWorld();
            source.EnsureField();
            if (session.State.Scene != SceneKind.Field || session.State.FieldId != run.ActiveField || VisitKey() != visit || !source.Events.CanAcceptMovementInput)
            {
                throw new NotSupportedException("The unavailable world request does not retain its authentic available pre-event field and control boundary.");
            }
            if (!source.CanCacheAutoPlayWorldUnavailableSource(eventId, run.ActiveObjectId, request))
            {
                throw new NotSupportedException("The unavailable world request has an unrepresented original source closure, issuing site, literal packet or predicate input.");
            }
            ValidateNativeBacktrackPredicates(signature, source.ReadAutoPlayConditions(eventId, run.ActiveObjectId), receipt.AllocationFingerprint, source.ReadAutoPlayWorldAbsenceFingerprint(receipt));
        }
        catch
        {
            restore(diagnostic);
            throw;
        }
        string guardKey = "world:" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(receipt, JsonOptions)));
        run.WorldUnavailabilityGuards[guardKey] = receipt;
        AutoPlayEventCoverage coverage = run.Coverage.Visits.Single(item => item.Visit.Key == visit).Events.Single(item => item.Event.Id == eventId);
        int attempts = coverage.UnavailableAttempts.Count;
        planner.MarkUnavailable(visit, eventId, new(signature, $"world:{receipt.Story}", guardKey, receipt.AllocationFingerprint, run.ActiveTargetId));
        if (coverage.UnavailableAttempts.Count != attempts)
        {
            run.LastProgressStep = run.Steps;
            ResetIdleDeadline();
        }
        RefreshUnavailable(getWorld());
        ClearNativeUnavailableInteraction();
        SaveBundle($"world-unavailable-return-{run.Steps:D10}", CaptureState());
        Status = "Restored the authentic pre-event state; the requested native world body is absent under the observed allocation conditions.";
    }
}
