using System.Collections.Immutable;
using System.Diagnostics;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticInvocationPool(uint DescriptorAddress, uint BankAddress, ushort Stride, int ActiveRows, bool Head, bool Tail);
public sealed record BattleDiagnosticInvocationOwners(int DescriptorPackets, int NonPumpDescriptorTasks, int PendingContacts, int Effects, bool EffectHead, bool AuxiliaryHead, int Modules, bool ModuleHead, int? Files, uint? ReturnedTaskPointer, bool DetachedReturnedTask, byte CameraMask, bool SoundBusy, bool ReplacementResource, bool ActionOrCommand, bool TimedOrReaction)
{
    public byte ActionHeaders { get; init; }
    public byte ContactRows { get; init; }
    public int? RetainedActiveHeader { get; init; }
    public int? RetainedActiveContact { get; init; }
    public bool ObservedOriginalEffect { get; init; }
    public uint? SelectedReturnedDescriptor { get; init; }
    public bool HeaderGraphicsBusy { get; init; }
    public ImmutableArray<BattleDiagnosticInvocationPool> OriginalPools { get; init; }
}
public sealed record BattleDiagnosticInvocationRelease(Guid LifecycleId, long Callback, long SimulationFrame, long Timestamp, BattleDiagnosticEffectDispatch Dispatch, NativeBattleActionHeaderSlot Header, NativeBattleDescriptorPacket? CompletionPacket, uint CompletionCallback, byte EventActorIndex, int CompletionPacketSlot, byte CompletionPacketSerial, uint CompletionPacketArgument, bool NativeEventCompleted, bool PresentationAcknowledged, bool HeaderCapturedBeforeReset, bool CurrentCompletionPacketGuaranteedRemoval, BattleDiagnosticInvocationOwners Owners, ImmutableArray<string> Blockers)
{
    public bool Released => NativeEventCompleted && PresentationAcknowledged && HeaderCapturedBeforeReset && CurrentCompletionPacketGuaranteedRemoval && Blockers.IsEmpty;
}

public sealed partial class BattleSession
{
    private sealed record DiagnosticInvocationPolicy(ushort Effect, string ActorId, byte? Command, int ActionId);
    private sealed record DiagnosticInvocationCompletion(BattleDiagnosticEffectDispatch Dispatch, NativeBattleActionHeaderSlot Header, NativeBattleDescriptorPacket? Packet, byte ActorIndex, uint Callback, int PacketSlot, byte PacketSerial, bool HeaderCaptured);
    private DiagnosticInvocationPolicy? diagnosticInvocationPolicy;
    private BattleDiagnosticEffectDispatch? diagnosticInvocationDispatch;
    public BattleDiagnosticInvocationRelease? LastDiagnosticInvocationCompletion { get; private set; }
    public event Action<BattleDiagnosticInvocationRelease>? DiagnosticInvocationReleased;

    public void ConfigureDiagnosticInvocationCompletion(ushort effect, string actorId, byte? command, int actionId)
    {
        if (diagnosticInvocationPolicy is not null || effect == 0 || string.IsNullOrWhiteSpace(actorId) || actionId < 0 || DiagnosticEffectDispatched is null)
        {
            throw new InvalidOperationException("Focal completion requires one explicit live diagnostic observer and invocation.");
        }
        diagnosticInvocationPolicy = new(effect, actorId, command, actionId);
    }

    private void ObserveDiagnosticInvocationDispatch(BattleDiagnosticEffectDispatch dispatch)
    {
        if (LastDiagnosticInvocationCompletion is { Released: true } || diagnosticInvocationPolicy is not { } policy || dispatch.RequestedEffect != policy.Effect || dispatch.ExecutedEffect != policy.Effect || dispatch.ActorId != policy.ActorId || dispatch.ActionId != policy.ActionId || policy.Command is byte command && dispatch.NativeCommand != command)
        {
            return;
        }
        if (diagnosticInvocationDispatch is not null)
        {
            throw new InvalidDataException("Single-use focal completion observed more than one matching native invocation.");
        }
        diagnosticInvocationDispatch = dispatch;
    }

    private DiagnosticInvocationCompletion? CaptureDiagnosticInvocationCompletion(NativeBattleGraphicsState graphics, uint callback)
    {
        if (diagnosticInvocationPolicy is null || diagnosticInvocationDispatch is not { NativeHeaderIndex: int header, NativeHeaderGeneration: long generation, NativeActorIndex: byte actor } dispatch || LastDiagnosticInvocationCompletion is { Released: true })
        {
            return null;
        }
        if (nativeActionScratch.EventActorB0F != actor)
        {
            return null;
        }
        NativeBattleActionHeaderSlot physicalHeader = nativeActionPackets.Headers[header];
        bool captured = physicalHeader.ReadByte(0) == actor && physicalHeader.ReadByte(1) == dispatch.NativeCommand && (physicalHeader.ReadByte(4) | physicalHeader.ReadByte(5) << 8) == dispatch.ActionId && (physicalHeader.ReadByte(6) | physicalHeader.ReadByte(7) << 8) == dispatch.RequestedEffect && dispatch.Serial <= diagnosticDispatchSerial && diagnosticHeaderGenerations is { } generations && generations.TryGetValue(header, out long actual) && actual == generation && generation > 0 && nativeActionScratch.EventActorB0F == actor && dispatch.Serial > 0;
        int slot = graphics.Descriptors.Queue.IsEmpty ? -1 : graphics.Descriptors.Queue[0];
        byte serial = slot < 0 ? (byte)0 : graphics.Descriptors.Packets[slot].Serial;
        return new(dispatch, physicalHeader, slot < 0 ? null : graphics.Descriptors.Packets[slot], actor, callback, slot, serial, captured);
    }

    private void ObserveDiagnosticInvocationCompletion(DiagnosticInvocationCompletion? attempt, bool completed, bool acknowledged)
    {
        if (attempt is null || !attempt.HeaderCaptured && LastDiagnosticInvocationCompletion is { HeaderCapturedBeforeReset: true })
        {
            return;
        }
        NativeBattleGraphicsState graphics = NativeGraphics ?? throw new InvalidDataException("Focal completion lost its actual graphical owner.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Focal completion lost its returned-task owner.");
        int? files = NativeFileIo?.ActiveCount;
        bool exactPacket = attempt.PacketSlot >= 0 && graphics.Descriptors.Queue.Length == 1 && graphics.Descriptors.Queue[0] == attempt.PacketSlot && graphics.Descriptors.Packets[attempt.PacketSlot] is { Command: 10 } packet && packet.Serial == attempt.PacketSerial && packet.Argument == attempt.Callback;
        BattleDiagnosticInvocationOwners owners = new(graphics.Descriptors.Queue.Length, graphics.Descriptors.Tasks.Count(task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0), graphics.PendingContacts.Length, graphics.Effects.PublishedCount, graphics.Effects.Head is not null, graphics.Auxiliary.Head is not null, graphics.ModuleEffects.PublishedCount, graphics.ModuleEffects.Head is not null, files, memory.ReturnedTaskPointer1D96AAC, memory.DetachedReturnedTask is not null, NativeCameraTaskMask, NativeOriginalSoundBusy(), graphics.Models.Any(model => (model.Flags0 & 2) != 0 && (model.ResourceMagicId is not null || model.ActiveStagedResourceRow100 is not null)), ActiveAction is not null || actions.Count != 0 || nativeCommandBanks.BoundCommands.Any(), nativeTimedAction is not null || nativeActionCommitDepth != 0 || HasNativePendingReactions || pendingSlotsDeal is not null || nativeShotProcesses.Count != 0 || nativeDuelProcesses.Count != 0 || nativeRenzokukenProcesses.Count != 0)
        {
            ActionHeaders = nativeActionPackets.ActionCount,
            ContactRows = nativeActionPackets.ContactCount,
            RetainedActiveHeader = nativeActionPackets.ActiveHeaderIndex,
            RetainedActiveContact = nativeActionPackets.ActiveContactIndex,
            ObservedOriginalEffect = graphics.OriginalEffect is not null,
            SelectedReturnedDescriptor = graphics.OriginalEffect?.ReturnedDescriptor,
            HeaderGraphicsBusy = (graphics.Flags & 0x10) != 0,
            OriginalPools = graphics.OriginalEffect is { } selected ? selected.Pools.Select(pool => new BattleDiagnosticInvocationPool(pool.DescriptorAddress, pool.BankAddress, pool.Stride, pool.Slots.Count(task => (task.Flags & 1) != 0), pool.Head is not null, pool.Tail is not null)).ToImmutableArray() : []
        };
        ImmutableArray<string>.Builder blockers = ImmutableArray.CreateBuilder<string>();
        if (!completed)
        {
            blockers.Add("native-event-incomplete");
        }
        if (!acknowledged)
        {
            blockers.Add("presentation-not-acknowledged");
        }
        if (!attempt.HeaderCaptured)
        {
            blockers.Add("header-generation-or-event-owner-mismatch");
        }
        if (!exactPacket)
        {
            blockers.Add("executing-completion-packet-mismatch");
        }
        if (owners.NonPumpDescriptorTasks != 0 || owners.PendingContacts != 0)
        {
            blockers.Add("contact-or-descriptor");
        }
        if (owners.Effects != 0 || owners.EffectHead || owners.AuxiliaryHead || owners.Modules != 0 || owners.ModuleHead)
        {
            blockers.Add("effect-or-auxiliary-task");
        }
        if (owners.Files != 0 || owners.ReturnedTaskPointer != 0 || owners.DetachedReturnedTask || owners.CameraMask != 0 || owners.SoundBusy || owners.ReplacementResource)
        {
            blockers.Add("file-camera-sound-or-effect-resource");
        }
        if (!owners.ObservedOriginalEffect || owners.SelectedReturnedDescriptor != 0 || owners.HeaderGraphicsBusy || owners.OriginalPools.Any(pool => pool.ActiveRows != 0 || pool.Head || pool.Tail))
        {
            blockers.Add("selected-native-pool-or-header-graphics");
        }
        if (owners.ActionHeaders != 0 || owners.ContactRows != 0 || owners.ActionOrCommand || owners.TimedOrReaction)
        {
            blockers.Add("action-or-reaction");
        }
        LastDiagnosticInvocationCompletion = new(DiagnosticLifecycleId, NativeHostCallbacks ?? throw new NotSupportedException("Focal completion requires its actual native callback."), Frame, Stopwatch.GetTimestamp(), attempt.Dispatch, attempt.Header, attempt.Packet, attempt.Callback, attempt.ActorIndex, attempt.PacketSlot, attempt.PacketSerial, attempt.Callback, completed, acknowledged, attempt.HeaderCaptured, exactPacket, owners, blockers.ToImmutable());
        if (LastDiagnosticInvocationCompletion.Released)
        {
            DiagnosticInvocationReleased?.Invoke(LastDiagnosticInvocationCompletion);
        }
    }
}
