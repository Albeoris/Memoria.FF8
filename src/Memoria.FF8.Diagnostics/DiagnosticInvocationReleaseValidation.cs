using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static class DiagnosticInvocationReleaseValidation
{
    public static void Validate(BattleDiagnosticInvocationRelease release, BattleDiagnosticEffectDispatch focal, Guid lifecycle)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(focal);
        BattleDiagnosticEffectDispatch observed = release.Dispatch ?? throw new InvalidDataException("Focal release has no dispatch owner.");
        if (lifecycle == Guid.Empty || release.LifecycleId != lifecycle || release.Callback <= focal.Callback || release.SimulationFrame < 0 || release.Timestamp <= 0 || observed != focal || focal.Serial <= 0 || focal.NativeHeaderIndex is not (>= 0 and < NativeBattleActionPacketsState.HeaderCapacity) || focal.NativeHeaderGeneration is not > 0 || focal.NativeActorIndex is not byte actor || release.EventActorIndex != actor || release.CompletionCallback is not (0x47DDA0 or 0x47DE70 or 0x47E120) || release.CompletionPacketSlot is < 0 or >= 30 || release.CompletionPacketArgument != release.CompletionCallback || !release.NativeEventCompleted || !release.PresentationAcknowledged || !release.HeaderCapturedBeforeReset || !release.CurrentCompletionPacketGuaranteedRemoval || release.Blockers.IsDefault || !release.Blockers.IsEmpty)
        {
            throw new InvalidDataException("Focal release lacks its exact native completed event, acknowledged header generation, dispatch or executing completion packet.");
        }
        NativeBattleActionHeaderSlot header = release.Header ?? throw new InvalidDataException("Focal release has no pre-reset native header.");
        NativeBattleDescriptorPacket packet = release.CompletionPacket ?? throw new InvalidDataException("Focal release has no exact executing packet.");
        if (header.Payload.IsDefault || header.Payload.Length != 20 || (header.UnknownByteMask & 0xF3) != 0 || header.ReadByte(0) != actor || header.ReadByte(1) != focal.NativeCommand || (header.ReadByte(4) | header.ReadByte(5) << 8) != focal.ActionId || (header.ReadByte(6) | header.ReadByte(7) << 8) != focal.RequestedEffect || packet.Bytes.IsDefault || packet.Bytes.Length != 12 || (packet.UnknownByteMask & 0xFF) != 0 || packet.Serial != release.CompletionPacketSerial || packet.Command != 10 || packet.Argument != release.CompletionCallback || (packet.PriorityState & 15) != 0)
        {
            throw new InvalidDataException("Focal release header/descriptor bytes differ from their exact dispatch/current packet identities.");
        }
        BattleDiagnosticInvocationOwners owners = release.Owners ?? throw new InvalidDataException("Focal release has no actual native owner snapshot.");
        if (owners.ActionHeaders != 0 || owners.ContactRows != 0 || owners.DescriptorPackets != 1 || owners.NonPumpDescriptorTasks != 0 || owners.PendingContacts != 0 || owners.Effects != 0 || owners.EffectHead || owners.AuxiliaryHead || owners.Modules != 0 || owners.ModuleHead || owners.Files != 0 || owners.ReturnedTaskPointer != 0 || owners.DetachedReturnedTask || owners.CameraMask != 0 || owners.SoundBusy || owners.ReplacementResource || owners.ActionOrCommand || owners.TimedOrReaction || !owners.ObservedOriginalEffect || owners.SelectedReturnedDescriptor != 0 || owners.HeaderGraphicsBusy || owners.OriginalPools.IsDefault || owners.OriginalPools.Any(pool => pool is null || pool.ActiveRows != 0 || pool.Head || pool.Tail || pool.DescriptorAddress == 0 || pool.BankAddress == 0 || pool.Stride == 0))
        {
            throw new InvalidDataException("Focal release retains a live or unknown native owner; only its exact executing completion10 is admitted pending the guaranteed native removal.");
        }
    }
}
