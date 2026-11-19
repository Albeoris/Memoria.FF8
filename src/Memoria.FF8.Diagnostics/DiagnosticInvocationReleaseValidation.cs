using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class DiagnosticInvocationReleaseValidation
{
    public static void ValidateFollowupFocalRetirement(BattleDiagnosticInvocationRelease retirement, BattleDiagnosticEffectDispatch focal, Guid lifecycle)
    {
        ValidateIdentity(retirement, focal, lifecycle);
        bool textures = !retirement.PendingTextures.IsDefaultOrEmpty;
        if (retirement.Boundary != BattleDiagnosticInvocationRelease.GraphicsFrameBoundary || retirement.TerminalTextures is not null || !retirement.PacketRemoved || retirement.Released ||
            (textures ? retirement.Blockers is not ["effect-or-auxiliary-task", "action-or-reaction"] : retirement.Blockers is not ["action-or-reaction"]) ||
            retirement.Owners.AuxiliaryHead != textures || !retirement.Owners.ActionOrCommand || HasOutstandingOwners(retirement.Owners with { ActionOrCommand = false, AuxiliaryHead = false }, 0) || retirement.HeaderReference is null || retirement.EventSlot is null || retirement.CompletionToken <= 0 || retirement.PendingFollowups.IsDefaultOrEmpty || retirement.PendingFollowups.Length > 64)
        {
            throw new InvalidDataException("Follow-up retirement requires exact focal packet removal and only proved queued native player callbacks.");
        }
        ValidateGraphicsFrameCertificate(retirement, focal, lifecycle);
        ValidateFollowupPendingTextures(retirement, focal);
        HashSet<(BattleActorReference Actor, byte Stage)> pending = [];
        foreach (BattleDiagnosticInvocationPendingCallback callback in retirement.PendingFollowups)
        {
            if (callback is null || callback.Actor is not BattlePartyActorReference || callback.Stage is not (2 or 3) || callback.QueuedCallback < focal.Callback || callback.QueuedCallback > retirement.Callback || !pending.Add((callback.Actor, callback.Stage)))
            {
                throw new InvalidDataException("Focal follow-up callback queue identity or timing is ambiguous.");
            }
        }
    }

    public static void ValidateFollowup(BattleDiagnosticInvocationFollowupRelease release, BattleDiagnosticEffectDispatch focal, Guid lifecycle)
    {
        ArgumentNullException.ThrowIfNull(release);
        BattleDiagnosticInvocationRelease retirement = release.FocalRetirement ?? throw new InvalidDataException("Follow-up release has no focal retirement.");
        ValidateFollowupFocalRetirement(retirement, focal, lifecycle);
        BattleDiagnosticInvocationFinalDrain drain = release.FinalDrain ?? throw new InvalidDataException("Follow-up release has no observed final boundary.");
        if (drain.LifecycleId != lifecycle || drain.Callback <= retirement.Callback || drain.SimulationFrame < retirement.SimulationFrame || drain.GraphicsCallback <= retirement.GraphicsCallback || drain.Timestamp <= retirement.Timestamp || drain.FocalDispatch is null || !DiagnosticEffectDispatchEvidence.Matches(drain.FocalDispatch, focal) || drain.Owners is null || HasOutstandingOwners(release.QuiescentTextures is null ? drain.Owners : drain.Owners with { AuxiliaryHead = false }, 0) || release.Callbacks.IsDefault || release.Callbacks.Length != retirement.PendingFollowups.Length)
        {
            throw new InvalidDataException("Follow-up drain changed focal lineage or retains actual native owners.");
        }
        HashSet<(BattleActorReference Actor, byte Stage)> completed = [];
        foreach (BattleDiagnosticInvocationFollowupCallback callback in release.Callbacks)
        {
            if (callback is null || callback.Actor is not BattlePartyActorReference || callback.Stage is not (2 or 3) || !completed.Add((callback.Actor, callback.Stage)) || !retirement.PendingFollowups.Any(pending => pending.Actor == callback.Actor && pending.Stage == callback.Stage && pending.QueuedCallback == callback.QueuedCallback) || callback.StartedCallback < retirement.Callback || callback.CompletedCallback < callback.StartedCallback || callback.CompletedCallback > drain.Callback)
            {
                throw new InvalidDataException("Follow-up drain lacks the actual once-queued, started and completed native callback witnesses.");
            }
        }
        if (release.QuiescentTextures is null)
        {
            ValidateFollowupTextureReleases(release);
        }
        else
        {
            ValidateFollowupQuiescence(release, focal);
        }
    }

    private static void ValidateFollowupQuiescence(BattleDiagnosticInvocationFollowupRelease release, BattleDiagnosticEffectDispatch focal)
    {
        BattleDiagnosticInvocationFollowupQuiescence proof = release.QuiescentTextures!;
        BattleComponentTextureQuiescenceObservation observation = proof.Observation ?? throw new InvalidDataException("Resident followup has no native fixed-point observation.");
        BattleDiagnosticInvocationFinalDrain boundary = release.FinalDrain;
        ValidatePlayerStage2NoOp(proof.NoOp, release);
        if (release.TextureReleases.IsDefault || !release.TextureReleases.IsEmpty || !boundary.Owners.AuxiliaryHead || release.Callbacks is not [{ Actor: BattlePartyActorReference actor, Stage: 2 }] || actor.ActorId != focal.ActorId ||
            proof.DispatchSerial != focal.Serial || proof.HeaderGeneration != focal.NativeHeaderGeneration || proof.HeaderGeneration <= 0 || !observation.IsEligible || observation.Effect != focal.RequestedEffect || observation.GraphicsCallback != boundary.GraphicsCallback ||
            observation.ResidentFingerprint is not { Length: 64 } || observation.ResidentFingerprint.Any(character => !Uri.IsHexDigit(character)) ||
            (observation.Owners with { OriginalPools = [] }) != (boundary.Owners with { OriginalPools = [] }) || !observation.Owners.OriginalPools.SequenceEqual(boundary.Owners.OriginalPools) ||
            proof.ResidentTextures.IsDefaultOrEmpty || proof.ResidentTextures.Any(texture => texture is null) || proof.ResidentTextures.Length != release.FocalRetirement.PendingTextures.Length || proof.ResidentTextures.Length != observation.Textures.Tasks.Length || proof.ResidentTextures.Select(texture => texture.Slot).Distinct().Count() != proof.ResidentTextures.Length)
        {
            throw new InvalidDataException("Resident followup requires exact focal lineage, completed caster stage2 and only the proved conditional texture owners.");
        }
        foreach (BattleDiagnosticInvocationPendingTexture texture in proof.ResidentTextures)
        {
            BattleDiagnosticTextureTask? row = observation.Textures.Tasks.SingleOrDefault(row => row.Slot == texture.Slot);
            if (!release.FocalRetirement.PendingTextures.Contains(texture) || row is null || row.ModelActorId != texture.Actor.ActorId || row.ModelIndex != texture.NativeActorIndex || row.CapturedSequence != texture.CapturedSequence || row.ModelSequence != texture.Sequence || row.ProgramOffset != texture.ProgramOffset || row.ProgramLength != texture.ProgramLength || row.Deadline != texture.Deadline || row.ModelFlags0 is not ushort flags || (flags & 2) != (texture.ModelFlags0 & 2) || row.ResourceFlags44 is not ushort resource || (resource & 0x40) != (texture.ResourceFlags44 & 0x40))
            {
                throw new InvalidDataException("Resident followup changed its originally captured texture/program/model lineage.");
            }
        }
    }

    private static void ValidateFollowupPendingTextures(BattleDiagnosticInvocationRelease retirement, BattleDiagnosticEffectDispatch focal)
    {
        if (retirement.PendingTextures.IsDefault || retirement.PendingTextures.Length > 16)
        {
            throw new InvalidDataException("Follow-up textures require a bounded original auxiliary owner inventory.");
        }
        HashSet<int> slots = [];
        foreach (BattleDiagnosticInvocationPendingTexture texture in retirement.PendingTextures)
        {
            if (texture is null || texture.Slot is < 0 or >= 16 || !slots.Add(texture.Slot) || texture.Actor is null || texture.Actor.ActorId != focal.ActorId || focal.ActorSlot is not { Side: BattleSide.Allies } || texture.NativeActorIndex != focal.NativeActorIndex || texture.NativeActorIndex > 2 ||
                texture.ProgramOffset < 0 || texture.ProgramOffset >= texture.ProgramLength || texture.ProgramSha256 is not { Length: 64 } || texture.ProgramSha256.Any(character => !Uri.IsHexDigit(character)) || texture.Deadline != 255 ||
                (texture.ModelFlags0 & 2) == 0 || (texture.ResourceFlags44 & 0x40) != 0 || texture.Sequence != texture.CapturedSequence)
            {
                throw new InvalidDataException("Follow-up texture is not an exact focal-caster-owned sequence-held native animation.");
            }
        }
    }

    private static void ValidateFollowupTextureReleases(BattleDiagnosticInvocationFollowupRelease release)
    {
        BattleDiagnosticInvocationRelease retirement = release.FocalRetirement;
        BattleDiagnosticInvocationFinalDrain drain = release.FinalDrain;
        if (release.TextureReleases.IsDefault || release.TextureReleases.Length != retirement.PendingTextures.Length)
        {
            throw new InvalidDataException("Follow-up texture drain lacks its actual retirement witnesses.");
        }
        HashSet<int> released = [];
        foreach (BattleDiagnosticInvocationTextureRelease texture in release.TextureReleases)
        {
            if (texture is null || texture.Texture is null || !released.Add(texture.Texture.Slot) || !retirement.PendingTextures.Contains(texture.Texture) || texture.Callback <= retirement.Callback || texture.Callback > drain.Callback || texture.SimulationFrame < retirement.SimulationFrame || texture.SimulationFrame > drain.SimulationFrame || texture.GraphicsCallback < retirement.GraphicsCallback || texture.GraphicsCallback >= drain.GraphicsCallback || texture.Timestamp <= retirement.Timestamp || texture.Timestamp > drain.Timestamp ||
                !texture.TextureReset || !texture.BlinkRestored || (texture.ModelFlags0 & 2) != 0 && (texture.ResourceFlags44 & 0x40) == 0 && texture.Sequence == texture.Texture.CapturedSequence)
            {
                throw new InvalidDataException("Follow-up texture lacks its exact actual native release and cleanup within the focal lineage.");
            }
        }
    }

    public static void Validate(BattleDiagnosticInvocationRelease release, BattleDiagnosticEffectDispatch focal, Guid lifecycle)
    {
        ValidateIdentity(release, focal, lifecycle);
        if (release.Boundary == BattleDiagnosticInvocationRelease.TerminalTextureBoundary)
        {
            ValidateGraphicsFrameCertificate(release, focal, lifecycle);
            ValidateTerminalTextures(release, focal, lifecycle);
            return;
        }
        if (release.TerminalTextures is not null)
        {
            throw new InvalidDataException("Standalone terminal textures cannot be attached to another completion mode.");
        }
        if (!release.Blockers.IsEmpty)
        {
            throw new InvalidDataException("Focal release retains explicit native blockers.");
        }
        if (release.Boundary == BattleDiagnosticInvocationRelease.ExecutingBoundary)
        {
            if (release.PacketRemoved || release.RemovedCompletionPacket is not null || release.ExecutingCompletion is not null || HasOutstandingOwners(release.Owners, 1))
            {
                throw new InvalidDataException("Executing completion requires its exact still-queued10 and no other native owner.");
            }
            return;
        }
        if (release.Boundary != BattleDiagnosticInvocationRelease.GraphicsFrameBoundary || !release.PacketRemoved || HasOutstandingOwners(release.Owners, 0))
        {
            throw new InvalidDataException("Completed graphics-frame release requires its removed packet and all actual native owners empty.");
        }
        ValidateGraphicsFrameCertificate(release, focal, lifecycle);
    }

    private static void ValidateGraphicsFrameCertificate(BattleDiagnosticInvocationRelease release, BattleDiagnosticEffectDispatch focal, Guid lifecycle)
    {
        BattleDiagnosticInvocationRelease certificate = release.ExecutingCompletion ?? throw new InvalidDataException("Completed graphics-frame release lacks its original exact executing certificate.");
        ValidateIdentity(certificate, focal, lifecycle);
        NativeBattleDescriptorPacket originalPacket = certificate.CompletionPacket!;
        NativeBattleDescriptorPacket focalPacket = release.CompletionPacket!;
        if (certificate.Boundary != BattleDiagnosticInvocationRelease.ExecutingBoundary || certificate.PacketRemoved || certificate.RemovedCompletionPacket is not null || certificate.ExecutingCompletion is not null || certificate.TerminalTextures is not null || certificate.Owners.DescriptorPackets != 1 || certificate.Blockers.IsEmpty || !HasOutstandingOwners(certificate.Owners, 1) || certificate.Callback != release.Callback || certificate.SimulationFrame != release.SimulationFrame || certificate.Timestamp > release.Timestamp || certificate.GraphicsCallback is not >= 0 || release.GraphicsCallback != certificate.GraphicsCallback + 1 || certificate.CompletionPacketSlot != release.CompletionPacketSlot || certificate.CompletionPacketSerial != release.CompletionPacketSerial || certificate.CompletionCallback != release.CompletionCallback || certificate.CompletionPacketArgument != release.CompletionPacketArgument || certificate.CompletionToken != release.CompletionToken || certificate.HeaderReference != release.HeaderReference || !SameOriginalHeader(certificate.Header, release.Header) || !originalPacket.Bytes.AsSpan().SequenceEqual(focalPacket.Bytes.AsSpan()) || originalPacket.UnknownByteMask != focalPacket.UnknownByteMask || originalPacket.PriorityState == 0)
        {
            throw new InvalidDataException("Deferred release changed its same-callback native event/header/packet certificate or retried across graphics frames.");
        }
        NativeBattleDescriptorPacket retired = release.RemovedCompletionPacket ?? throw new InvalidDataException("Deferred release has no observed retired physical packet.");
        if (retired.UnknownByteMask != originalPacket.UnknownByteMask || retired.Bytes.IsDefault || !retired.Bytes.AsSpan().SequenceEqual(originalPacket.Bytes.SetItem(1, 0).AsSpan()))
        {
            throw new InvalidDataException("Deferred completion did not retain the exact native RemoveFirst byte1 reset, or its physical slot was reused.");
        }
    }

    private static void ValidateTerminalTextures(BattleDiagnosticInvocationRelease release, BattleDiagnosticEffectDispatch focal, Guid lifecycle)
    {
        BattleDiagnosticTerminalTextureRelease proof = release.TerminalTextures ?? throw new InvalidDataException("Standalone terminal texture release lacks its actual native proof.");
        BattleDiagnosticTerminalHoldState hold = proof.Hold ?? throw new InvalidDataException("Standalone terminal texture release lacks its declared victory hold.");
        hold.Declaration.Validate();
        if (!release.PacketRemoved || !release.Owners.AuxiliaryHead || HasOutstandingOwners(release.Owners with { AuxiliaryHead = false }, 0) || release.Blockers.Length != 1 || release.Blockers[0] != "effect-or-auxiliary-task" || focal.NativeCommand != 16 || focal.ActionId is not (53 or 56) || focal.RequestedEffect != (focal.ActionId == 53 ? 288 : 85) || hold.Declaration.Candidate != BattleOutcome.Victory || hold.Rearms != 0 || hold.HoldId == Guid.Empty || hold.Declaration.PartyActorIds.Length != 1 || hold.Declaration.PartyActorIds[0] != focal.ActorId || hold.Receipts.IsDefault)
        {
            throw new InvalidDataException("Standalone terminal textures require one declared held destructive Slots victory, exact packet retirement and no other native owner.");
        }
        if (hold.Phase == BattleDiagnosticTerminalHoldPhase.Armed)
        {
            BattleDiagnosticNaturalVictory victory = proof.NaturalVictory ?? throw new InvalidDataException("Prospective terminal textures lack their actual native natural-victory witness.");
            if (hold.Sequence != 0 || !hold.Receipts.IsEmpty || hold.HeldCallback is not null || hold.HeldFrame is not null || !victory.IsNaturalVictory || victory.LifecycleId != lifecycle || victory.Callback != release.Callback || victory.SimulationFrame != release.SimulationFrame || !victory.Combatants.Any(actor => actor.Id == focal.ActorId && actor.IsPlayer && actor.NativeIndex == focal.NativeActorIndex && (focal.ActorSlot is null || actor.Slot == focal.ActorSlot) && actor.CurrentHp > 0 && (actor.SavedStatus & 5) == 0 && (actor.BattleStatus & 0x80010000) == 0))
            {
                throw new InvalidDataException("Prospective terminal textures changed the pristine declaration or same-frame ordinary native victory facts.");
            }
        }
        else
        {
            if (hold.Phase != BattleDiagnosticTerminalHoldPhase.Held || hold.Sequence != 1 || hold.Receipts.Length != 1 || hold.HeldCallback is not long heldCallback || heldCallback > release.Callback || release.Callback - heldCallback > hold.Declaration.MaximumCallbacks || hold.HeldFrame is not long heldFrame || heldFrame > release.SimulationFrame || proof.NaturalVictory is not null)
            {
                throw new InvalidDataException("Standalone terminal textures changed their held admission boundary.");
            }
            ValidateTerminalTextureAdmission(release, hold.Receipts[0]);
            if (hold.Receipts[0].Callback != hold.HeldCallback || hold.Receipts[0].Frame != hold.HeldFrame)
            {
                throw new InvalidDataException("Standalone terminal textures changed their actual held clocks.");
            }
        }
        DiagnosticTextureQuiescenceValidation.Validate(proof.Textures);
        if (proof.Textures.Tasks.Any(task => task.ModelIndex != focal.NativeActorIndex || (task.ModelActorId is null ? focal.NativeActorIndex is null : task.ModelActorId != focal.ActorId)))
        {
            throw new InvalidDataException("Standalone terminal textures cannot include a foreign model owner.");
        }
    }

    public static void ValidateTerminalTextureAdmission(BattleDiagnosticInvocationRelease release, BattleDiagnosticTerminalHoldReceipt admission)
    {
        BattleDiagnosticTerminalHoldState hold = release.TerminalTextures?.Hold ?? throw new InvalidDataException("Terminal texture admission has no sealed main proof.");
        BattleDiagnosticEffectDispatch focal = release.Dispatch;
        bool prospective = hold.Phase == BattleDiagnosticTerminalHoldPhase.Armed;
        if (admission is null || admission.Kind != "held" || admission.Candidate != BattleOutcome.Victory || admission.HoldId != hold.HoldId || admission.LifecycleId != release.LifecycleId || admission.Sequence != 1 || admission.Callback < 0 || admission.Frame < 0 || (prospective ? admission.Callback <= release.Callback || admission.Frame < release.SimulationFrame || admission.Callback - release.Callback > hold.Declaration.MaximumCallbacks : admission.Callback > release.Callback || admission.Frame > release.SimulationFrame) || admission.NativeResult is > 0 || admission.RestoreCount != 0 || admission.ResetCount != 0 || admission.Random is null || admission.Party is not { Length: 1 } || admission.Party[0] is not { } caster || caster.Id != focal.ActorId || caster.NativeIndex != focal.NativeActorIndex || focal.ActorSlot is not null && caster.Slot != focal.ActorSlot || caster.CurrentHp <= 0 || caster.CurrentHp > caster.MaximumHp || (caster.Statuses0 & 5) != 0 || (caster.Statuses1 & 0x80010000) != 0)
        {
            throw new InvalidDataException("Standalone texture victory admission changed its genuine normal-pass held receipt or living caster.");
        }
    }

    private static void ValidateIdentity(BattleDiagnosticInvocationRelease release, BattleDiagnosticEffectDispatch focal, Guid lifecycle)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(focal);
        BattleDiagnosticEffectDispatch observed = release.Dispatch ?? throw new InvalidDataException("Focal release has no dispatch owner.");
        if (lifecycle == Guid.Empty || release.LifecycleId != lifecycle || release.Callback <= focal.Callback || release.SimulationFrame < 0 || release.Timestamp <= 0 || !DiagnosticEffectDispatchEvidence.Matches(observed, focal) || focal.Serial <= 0 || focal.NativeHeaderGeneration is not > 0 || !HasReleaseIdentity(release, focal) || release.CompletionCallback is not (0x47DDA0 or 0x47DE70 or 0x47E120) || release.CompletionPacketSlot is < 0 or >= 30 || release.CompletionPacketArgument != release.CompletionCallback || !release.NativeEventCompleted || !release.PresentationAcknowledged || !release.HeaderCapturedBeforeReset || !release.CurrentCompletionPacketGuaranteedRemoval || release.Blockers.IsDefault)
        {
            throw new InvalidDataException("Focal release lacks its exact native completed event, acknowledged header generation, dispatch or executing completion packet.");
        }
        NativeBattleDescriptorPacket packet = release.CompletionPacket ?? throw new InvalidDataException("Focal release has no exact executing packet.");
        if (!MatchesOriginalHeader(release.Header, focal) || packet.Bytes.IsDefault || packet.Bytes.Length != 12 || (packet.UnknownByteMask & 0xFF) != 0 || packet.Serial != release.CompletionPacketSerial || packet.Command != 10 || packet.Argument != release.CompletionCallback || (packet.PriorityState & 15) != 0 || packet.PriorityState == 0)
        {
            throw new InvalidDataException("Focal release header/descriptor bytes differ from their exact dispatch/current packet identities.");
        }
        ArgumentNullException.ThrowIfNull(release.Owners);
    }

    private static bool HasReleaseIdentity(BattleDiagnosticInvocationRelease release, BattleDiagnosticEffectDispatch focal)
    {
        if (release.EventActorIndex != focal.NativeActorIndex)
        {
            return false;
        }
        if (release.HeaderReference is null && focal.HeaderReference is null && release.CompletionToken == 0)
        {
            return focal.NativeActorIndex.HasValue && focal.NativeHeaderIndex is >= 0 and < NativeBattleRetainedActionPackets.HeaderCapacity;
        }
        return release.HeaderReference is { } current && current == focal.HeaderReference && release.EventSlot is { } slot && slot == focal.ActorSlot && release.CompletionToken > 0;
    }

    private static bool MatchesOriginalHeader(NativeBattleRetainedActionHeaderSlot? header, BattleDiagnosticEffectDispatch focal)
    {
        if (focal.NativeHeaderIndex is null)
        {
            return header is null;
        }
        if (header is null || focal.NativeHeaderIndex is < 0 or >= NativeBattleRetainedActionPackets.HeaderCapacity ||
            focal.HeaderReference is { } reference && reference.Ordinal != focal.NativeHeaderIndex ||
            header.Payload.IsDefault || header.Payload.Length != 20 || (header.UnknownByteMask & 0xF2) != 0)
        {
            return false;
        }
        byte? originalActor = (header.UnknownByteMask & 1) == 0 ? header.ReadOriginalByte(0) : null;
        return originalActor == focal.NativeActorIndex && header.ReadOriginalByte(1) == focal.NativeCommand &&
            (header.ReadOriginalByte(4) | header.ReadOriginalByte(5) << 8) == focal.ActionId &&
            (header.ReadOriginalByte(6) | header.ReadOriginalByte(7) << 8) == focal.RequestedEffect;
    }

    private static bool SameOriginalHeader(NativeBattleRetainedActionHeaderSlot? first, NativeBattleRetainedActionHeaderSlot? last)
    {
        return first is null ? last is null : last is not null && first.Payload.AsSpan().SequenceEqual(last.Payload.AsSpan()) &&
            first.UnknownByteMask == last.UnknownByteMask && first.FirstContactIndex == last.FirstContactIndex && first.Data == last.Data;
    }

    private static bool HasOutstandingOwners(BattleDiagnosticInvocationOwners owners, int descriptors)
    {
        return owners.ActionHeaders != 0 || owners.ContactRows != 0 || owners.DescriptorPackets != descriptors || owners.NonPumpDescriptorTasks != 0 || owners.PendingContacts != 0 || owners.Effects != 0 || owners.EffectHead || owners.AuxiliaryHead || owners.Modules != 0 || owners.ModuleHead || owners.Files != 0 || owners.ReturnedTaskPointer != 0 || owners.DetachedReturnedTask || owners.CameraMask != 0 || owners.SoundBusy || owners.ReplacementResource || owners.ActionOrCommand || owners.TimedOrReaction || !owners.ObservedOriginalEffect || owners.SelectedReturnedDescriptor != 0 || owners.HeaderGraphicsBusy || owners.OriginalPools.IsDefault || owners.OriginalPools.Any(pool => pool is null || pool.ActiveRows != 0 || pool.Head || pool.Tail || pool.DescriptorAddress == 0 || pool.BankAddress == 0 || pool.Stride == 0);
    }
}
