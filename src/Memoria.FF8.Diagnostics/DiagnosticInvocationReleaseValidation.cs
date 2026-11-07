using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static class DiagnosticInvocationReleaseValidation
{
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
        if (release.Boundary == BattleDiagnosticInvocationRelease.AftercareBoundary)
        {
            if (!release.PacketRemoved)
            {
                throw new InvalidDataException("Scoped aftercare begins only after actual main packet retirement.");
            }
            ValidateGraphicsFrameCertificate(release, focal, lifecycle);
            DiagnosticInvocationAftercareValidation.ValidatePlan(release, focal, lifecycle);
            return;
        }
        if (release.Aftercare is not null)
        {
            throw new InvalidDataException("Aftercare cannot be attached to an ordinary completion mode.");
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
        if (certificate.Boundary != BattleDiagnosticInvocationRelease.ExecutingBoundary || certificate.PacketRemoved || certificate.RemovedCompletionPacket is not null || certificate.ExecutingCompletion is not null || certificate.Aftercare is not null || certificate.TerminalTextures is not null || certificate.Owners.DescriptorPackets != 1 || certificate.Blockers.IsEmpty || !HasOutstandingOwners(certificate.Owners, 1) || certificate.Callback != release.Callback || certificate.SimulationFrame != release.SimulationFrame || certificate.Timestamp > release.Timestamp || certificate.GraphicsCallback is not >= 0 || release.GraphicsCallback != certificate.GraphicsCallback + 1 || certificate.CompletionPacketSlot != release.CompletionPacketSlot || certificate.CompletionPacketSerial != release.CompletionPacketSerial || certificate.CompletionCallback != release.CompletionCallback || certificate.CompletionPacketArgument != release.CompletionPacketArgument || !certificate.Header.Payload.AsSpan().SequenceEqual(release.Header.Payload.AsSpan()) || certificate.Header.UnknownByteMask != release.Header.UnknownByteMask || certificate.Header.FirstContactIndex != release.Header.FirstContactIndex || certificate.Header.Data != release.Header.Data || !originalPacket.Bytes.AsSpan().SequenceEqual(focalPacket.Bytes.AsSpan()) || originalPacket.UnknownByteMask != focalPacket.UnknownByteMask || originalPacket.PriorityState == 0)
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
        if (!release.PacketRemoved || release.Aftercare is not null || !release.Owners.AuxiliaryHead || HasOutstandingOwners(release.Owners with { AuxiliaryHead = false }, 0) || release.Blockers.Length != 1 || release.Blockers[0] != "effect-or-auxiliary-task" || focal.NativeCommand != 16 || focal.ActionId is not (53 or 56) || focal.RequestedEffect != (focal.ActionId == 53 ? 288 : 85) || hold.Declaration.Candidate != BattleOutcome.Victory || hold.Rearms != 0 || hold.HoldId == Guid.Empty || hold.Declaration.PartyActorIds.Length != 1 || hold.Declaration.PartyActorIds[0] != focal.ActorId || hold.Receipts.IsDefault)
        {
            throw new InvalidDataException("Standalone terminal textures require one declared held destructive Slots victory, exact packet retirement and no other native owner.");
        }
        if (hold.Phase == BattleDiagnosticTerminalHoldPhase.Armed)
        {
            BattleDiagnosticNaturalVictory victory = proof.NaturalVictory ?? throw new InvalidDataException("Prospective terminal textures lack their actual native natural-victory witness.");
            if (hold.Sequence != 0 || !hold.Receipts.IsEmpty || hold.HeldCallback is not null || hold.HeldFrame is not null || !victory.IsNaturalVictory || victory.LifecycleId != lifecycle || victory.Callback != release.Callback || victory.SimulationFrame != release.SimulationFrame || !victory.Combatants.Any(actor => actor.Id == focal.ActorId && actor.IsPlayer && actor.NativeIndex == focal.NativeActorIndex && actor.CurrentHp > 0 && (actor.SavedStatus & 5) == 0 && (actor.BattleStatus & 0x80010000) == 0))
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
        if (proof.Textures.Tasks.Any(task => task.ModelIndex != focal.NativeActorIndex))
        {
            throw new InvalidDataException("Standalone terminal textures cannot include a foreign model owner.");
        }
        DiagnosticInvocationAftercareValidation.ValidateEmptyCommandBanks(proof.CommandBanks);
    }

    public static void ValidateTerminalTextureAdmission(BattleDiagnosticInvocationRelease release, BattleDiagnosticTerminalHoldReceipt admission)
    {
        BattleDiagnosticTerminalHoldState hold = release.TerminalTextures?.Hold ?? throw new InvalidDataException("Terminal texture admission has no sealed main proof.");
        BattleDiagnosticEffectDispatch focal = release.Dispatch;
        bool prospective = hold.Phase == BattleDiagnosticTerminalHoldPhase.Armed;
        if (admission is null || admission.Kind != "held" || admission.Candidate != BattleOutcome.Victory || admission.HoldId != hold.HoldId || admission.LifecycleId != release.LifecycleId || admission.Sequence != 1 || admission.Callback < 0 || admission.Frame < 0 || (prospective ? admission.Callback <= release.Callback || admission.Frame < release.SimulationFrame || admission.Callback - release.Callback > hold.Declaration.MaximumCallbacks : admission.Callback > release.Callback || admission.Frame > release.SimulationFrame) || admission.NativeResult is > 0 || admission.RestoreCount != 0 || admission.ResetCount != 0 || admission.Random is null || admission.Party is not { Length: 1 } || admission.Party[0] is not { } caster || caster.Id != focal.ActorId || caster.NativeIndex != focal.NativeActorIndex || caster.CurrentHp <= 0 || caster.CurrentHp > caster.MaximumHp || (caster.Statuses0 & 5) != 0 || (caster.Statuses1 & 0x80010000) != 0)
        {
            throw new InvalidDataException("Standalone texture victory admission changed its genuine normal-pass held receipt or living caster.");
        }
    }

    private static void ValidateIdentity(BattleDiagnosticInvocationRelease release, BattleDiagnosticEffectDispatch focal, Guid lifecycle)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(focal);
        BattleDiagnosticEffectDispatch observed = release.Dispatch ?? throw new InvalidDataException("Focal release has no dispatch owner.");
        if (lifecycle == Guid.Empty || release.LifecycleId != lifecycle || release.Callback <= focal.Callback || release.SimulationFrame < 0 || release.Timestamp <= 0 || observed != focal || focal.Serial <= 0 || focal.NativeHeaderIndex is not (>= 0 and < NativeBattleActionPacketsState.HeaderCapacity) || focal.NativeHeaderGeneration is not > 0 || focal.NativeActorIndex is not byte actor || release.EventActorIndex != actor || release.CompletionCallback is not (0x47DDA0 or 0x47DE70 or 0x47E120) || release.CompletionPacketSlot is < 0 or >= 30 || release.CompletionPacketArgument != release.CompletionCallback || !release.NativeEventCompleted || !release.PresentationAcknowledged || !release.HeaderCapturedBeforeReset || !release.CurrentCompletionPacketGuaranteedRemoval || release.Blockers.IsDefault)
        {
            throw new InvalidDataException("Focal release lacks its exact native completed event, acknowledged header generation, dispatch or executing completion packet.");
        }
        NativeBattleActionHeaderSlot header = release.Header ?? throw new InvalidDataException("Focal release has no pre-reset native header.");
        NativeBattleDescriptorPacket packet = release.CompletionPacket ?? throw new InvalidDataException("Focal release has no exact executing packet.");
        if (header.Payload.IsDefault || header.Payload.Length != 20 || (header.UnknownByteMask & 0xF3) != 0 || header.ReadByte(0) != actor || header.ReadByte(1) != focal.NativeCommand || (header.ReadByte(4) | header.ReadByte(5) << 8) != focal.ActionId || (header.ReadByte(6) | header.ReadByte(7) << 8) != focal.RequestedEffect || packet.Bytes.IsDefault || packet.Bytes.Length != 12 || (packet.UnknownByteMask & 0xFF) != 0 || packet.Serial != release.CompletionPacketSerial || packet.Command != 10 || packet.Argument != release.CompletionCallback || (packet.PriorityState & 15) != 0 || packet.PriorityState == 0)
        {
            throw new InvalidDataException("Focal release header/descriptor bytes differ from their exact dispatch/current packet identities.");
        }
        ArgumentNullException.ThrowIfNull(release.Owners);
    }

    private static bool HasOutstandingOwners(BattleDiagnosticInvocationOwners owners, int descriptors)
    {
        return owners.ActionHeaders != 0 || owners.ContactRows != 0 || owners.DescriptorPackets != descriptors || owners.NonPumpDescriptorTasks != 0 || owners.PendingContacts != 0 || owners.Effects != 0 || owners.EffectHead || owners.AuxiliaryHead || owners.Modules != 0 || owners.ModuleHead || owners.Files != 0 || owners.ReturnedTaskPointer != 0 || owners.DetachedReturnedTask || owners.CameraMask != 0 || owners.SoundBusy || owners.ReplacementResource || owners.ActionOrCommand || owners.TimedOrReaction || !owners.ObservedOriginalEffect || owners.SelectedReturnedDescriptor != 0 || owners.HeaderGraphicsBusy || owners.OriginalPools.IsDefault || owners.OriginalPools.Any(pool => pool is null || pool.ActiveRows != 0 || pool.Head || pool.Tail || pool.DescriptorAddress == 0 || pool.BankAddress == 0 || pool.Stride == 0);
    }
}
