using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class DiagnosticInvocationReleaseValidation
{
    private static void ValidatePlayerStage2NoOp(BattleDiagnosticPlayerStage2NoOp? proof, BattleDiagnosticInvocationFollowupRelease release)
    {
        BattleDiagnosticInvocationRelease focal = release.FocalRetirement;
        if (proof is null || proof.Inputs is not { Active: true } inputs || inputs.Actor is null || inputs.Attacker != inputs.Actor || inputs.Actor.ActorId != focal.Dispatch.ActorId || inputs.NativeActorIndex > 2 || inputs.AttackerNativeIndex != inputs.NativeActorIndex || inputs.NativeActorIndex != focal.Dispatch.NativeActorIndex ||
            (inputs.SavedStatus & 5) != 0 || (inputs.BattleStatus & 0x4009) != 0 || (inputs.CombatantFlags & 0x4000) != 0 || proof.HeaderCount != 0 || proof.OperationCount != 1 || proof.ProducedCommands != 0 || proof.CompletionCallback != 0x47DDA0 || proof.CompletionToken != focal.CompletionToken + 1 ||
            release.Callbacks is not [{ Actor: BattlePartyActorReference actor, Stage: 2 } callback] || actor != inputs.Actor || proof.Callback != callback.StartedCallback || proof.CompletedCallback != callback.CompletedCallback || proof.Timestamp <= focal.Timestamp || proof.CompletedTimestamp is not long ended || ended < proof.Timestamp || ended > release.FinalDrain.Timestamp)
        {
            throw new InvalidDataException("Resident aftercare lacks its actual self-attacker player-stage2 return and completion-only native footprint.");
        }
    }

    public static void ValidateAftercarePresentationTail(BattleDiagnosticInvocationFollowupRelease release, BattleDiagnosticAftercareTail tail, int requiredFrames = 2)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(tail);
        BattleDiagnosticInvocationRelease focal = release.FocalRetirement ?? throw new InvalidDataException("Aftercare tail has no original focal retirement.");
        ValidateFollowup(release, focal.Dispatch, focal.LifecycleId);
        BattleDiagnosticInvocationFollowupQuiescence proof = release.QuiescentTextures ?? throw new InvalidDataException("A spanning tail requires explicit resident texture completion.");
        BattleDiagnosticPlayerStage2NoOp noOp = proof.NoOp!;
        BattleDiagnosticAftercarePresent first = tail.First ?? throw new InvalidDataException("Aftercare tail has no first actual presentation.");
        BattleDiagnosticAftercarePresent last = tail.Last ?? throw new InvalidDataException("Aftercare tail has no final actual presentation.");
        if (tail.TailPhase != BattleDiagnosticAftercareTail.Phase || requiredFrames < 2 || tail.PresentedFrames < requiredFrames || last.PresentedFrame - first.PresentedFrame + 1 != tail.PresentedFrames || first.PresentedFrame < 0 || first.Timestamp <= focal.Timestamp || last.Timestamp <= first.Timestamp ||
            first.Boundary is null || last.Boundary is null || first.Boundary.CallbackCompleted || !last.Boundary.CallbackCompleted || first.Boundary.Timestamp > first.Timestamp || last.Boundary.Timestamp > last.Timestamp || first.Timestamp >= noOp.Timestamp || last.Timestamp <= release.FinalDrain.Timestamp ||
            first.Boundary.Callback < focal.Callback || first.Boundary.Callback > noOp.Callback || last.Boundary.Callback < release.FinalDrain.Callback || first.Boundary.GraphicsCallback < focal.GraphicsCallback || last.Boundary.GraphicsCallback < release.FinalDrain.GraphicsCallback ||
            first.Boundary.ObservedFrames < 1 || last.Boundary.GraphicsCallback <= first.Boundary.GraphicsCallback || last.Boundary.ObservedFrames <= first.Boundary.ObservedFrames || last.Boundary.ObservedFrames - first.Boundary.ObservedFrames != last.Boundary.GraphicsCallback - first.Boundary.GraphicsCallback ||
            first.Boundary.ActionIssued != last.Boundary.ActionIssued || first.Boundary.CompletionIssued != focal.CompletionToken || first.Boundary.CompletionAcknowledged != focal.CompletionToken || last.Boundary.CompletionIssued != noOp.CompletionToken || last.Boundary.CompletionAcknowledged != noOp.CompletionToken)
        {
            throw new InvalidDataException("Spanning aftercare tail changed its actual presentations, no-op interval or native admission ledger.");
        }
        ValidateAftercareBoundary(first.Boundary, proof, focal);
        ValidateAftercareBoundary(last.Boundary, proof, focal);
    }

    private static void ValidateAftercareBoundary(BattleDiagnosticAftercareBoundary boundary, BattleDiagnosticInvocationFollowupQuiescence proof, BattleDiagnosticInvocationRelease focal)
    {
        if (boundary.LifecycleId != focal.LifecycleId || boundary.Timestamp <= focal.Timestamp || boundary.Callback < focal.Callback || boundary.SimulationFrame < focal.SimulationFrame || boundary.DispatchSerial != focal.Dispatch.Serial || boundary.HeaderGeneration != focal.Dispatch.NativeHeaderGeneration || boundary.HeaderGeneration <= 0 ||
            boundary.NativeActive != 0 || boundary.CanonicalReturnedTask != 0 || boundary.Inputs != proof.NoOp!.Inputs || boundary.Owners is null || boundary.Owners.ActionOrCommand == boundary.CallbackCompleted || !boundary.Owners.AuxiliaryHead || HasOutstandingOwners(boundary.Owners with { ActionOrCommand = false, AuxiliaryHead = false }, 0) ||
            boundary.ResidentFingerprint != proof.Observation.ResidentFingerprint || boundary.Textures is null || !boundary.Textures.IsTerminalQuiescent || boundary.Textures.Tasks.Length != proof.ResidentTextures.Length)
        {
            throw new InvalidDataException("Aftercare boundary retains foreign work or changed its exact conditional resident texture owners.");
        }
        foreach (BattleDiagnosticInvocationPendingTexture texture in proof.ResidentTextures)
        {
            BattleDiagnosticTextureTask? row = boundary.Textures.Tasks.SingleOrDefault(row => row.Slot == texture.Slot);
            if (row is null || row.ModelActorId != texture.Actor.ActorId || row.ModelIndex != texture.NativeActorIndex || row.CapturedSequence != texture.CapturedSequence || row.ModelSequence != texture.Sequence || row.ProgramOffset != texture.ProgramOffset || row.ProgramLength != texture.ProgramLength || row.Deadline != texture.Deadline)
            {
                throw new InvalidDataException("Aftercare interval changed an originally captured texture/model/program lineage.");
            }
        }
    }
}
