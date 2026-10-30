using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static class DiagnosticInvocationReleaseValidation
{
    public static void Validate(BattleDiagnosticInvocationRelease release, BattleDiagnosticEffectDispatch focal, Guid lifecycle)
    {
        ValidateIdentity(release, focal, lifecycle);
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
        if (certificate.Boundary != BattleDiagnosticInvocationRelease.ExecutingBoundary || certificate.PacketRemoved || certificate.RemovedCompletionPacket is not null || certificate.ExecutingCompletion is not null || certificate.Aftercare is not null || certificate.Owners.DescriptorPackets != 1 || certificate.Blockers.IsEmpty || !HasOutstandingOwners(certificate.Owners, 1) || certificate.Callback != release.Callback || certificate.SimulationFrame != release.SimulationFrame || certificate.Timestamp > release.Timestamp || certificate.GraphicsCallback is not >= 0 || release.GraphicsCallback != certificate.GraphicsCallback + 1 || certificate.CompletionPacketSlot != release.CompletionPacketSlot || certificate.CompletionPacketSerial != release.CompletionPacketSerial || certificate.CompletionCallback != release.CompletionCallback || certificate.CompletionPacketArgument != release.CompletionPacketArgument || !certificate.Header.Payload.AsSpan().SequenceEqual(release.Header.Payload.AsSpan()) || certificate.Header.UnknownByteMask != release.Header.UnknownByteMask || certificate.Header.FirstContactIndex != release.Header.FirstContactIndex || certificate.Header.Data != release.Header.Data || !originalPacket.Bytes.AsSpan().SequenceEqual(focalPacket.Bytes.AsSpan()) || originalPacket.UnknownByteMask != focalPacket.UnknownByteMask || originalPacket.PriorityState == 0)
        {
            throw new InvalidDataException("Deferred release changed its same-callback native event/header/packet certificate or retried across graphics frames.");
        }
        NativeBattleDescriptorPacket retired = release.RemovedCompletionPacket ?? throw new InvalidDataException("Deferred release has no observed retired physical packet.");
        if (retired.UnknownByteMask != originalPacket.UnknownByteMask || retired.Bytes.IsDefault || !retired.Bytes.AsSpan().SequenceEqual(originalPacket.Bytes.SetItem(1, 0).AsSpan()))
        {
            throw new InvalidDataException("Deferred completion did not retain the exact native RemoveFirst byte1 reset, or its physical slot was reused.");
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
