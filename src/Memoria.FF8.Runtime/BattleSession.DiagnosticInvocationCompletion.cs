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
    public const string ExecutingBoundary = "executing-completion";
    public const string GraphicsFrameBoundary = "completed-graphics-frame";
    public const string AftercareBoundary = "completed-graphics-frame-aftercare";
    public BattleDiagnosticInvocationAftercarePlan? Aftercare { get; init; }
    public string Boundary { get; init; } = ExecutingBoundary;
    public bool PacketRemoved { get; init; }
    public long? GraphicsCallback { get; init; }
    public NativeBattleDescriptorPacket? RemovedCompletionPacket { get; init; }
    public BattleDiagnosticInvocationRelease? ExecutingCompletion { get; init; }
    public bool Released => NativeEventCompleted && PresentationAcknowledged && HeaderCapturedBeforeReset && CurrentCompletionPacketGuaranteedRemoval && (Blockers.IsEmpty || Aftercare is not null);
}

public sealed partial class BattleSession
{
    private sealed record DiagnosticInvocationPolicy(ushort Effect, string ActorId, byte? Command, int ActionId);
    private sealed record DiagnosticInvocationCompletion(BattleDiagnosticEffectDispatch Dispatch, NativeBattleActionHeaderSlot Header, NativeBattleDescriptorPacket? Packet, byte ActorIndex, uint Callback, int PacketSlot, byte PacketSerial, bool HeaderCaptured, Guid LifecycleId, long SimulationFrame, long HostCallback, long GraphicsCallback, long DispatchSerial);
    private DiagnosticInvocationPolicy? diagnosticInvocationPolicy;
    private BattleDiagnosticEffectDispatch? diagnosticInvocationDispatch;
    private DiagnosticInvocationCompletion? diagnosticPendingFrameAttempt;
    private BattleDiagnosticInvocationRelease? diagnosticPendingFrameCertificate;
    private NativeBattleDescriptorPacket? diagnosticPendingRetiredPacket;
    public object? LastDiagnosticInvocationPendingOwners { get; private set; }
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
        ObserveDiagnosticInvocationAftercareEffectDispatch();
        if (diagnosticPendingFrameAttempt is not null)
        {
            LastDiagnosticInvocationPendingOwners = new { Reason = "dispatch-before-graphics-frame-end", dispatch.Serial, FocalSerial = diagnosticPendingFrameAttempt.Dispatch.Serial };
            diagnosticPendingFrameAttempt = null;
            diagnosticPendingFrameCertificate = null;
            diagnosticPendingRetiredPacket = null;
        }
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
        return new(dispatch, physicalHeader, slot < 0 ? null : graphics.Descriptors.Packets[slot], actor, callback, slot, serial, captured, DiagnosticLifecycleId, Frame, NativeHostCallbacks ?? throw new NotSupportedException("Focal completion requires its actual native callback."), graphics.Callback, diagnosticDispatchSerial);
    }

    private void ObserveDiagnosticInvocationCompletion(DiagnosticInvocationCompletion? attempt, bool completed, bool acknowledged, BattleDiagnosticInvocationRelease? executingCertificate = null, NativeBattleDescriptorPacket? retiredWitness = null)
    {
        if (attempt is null || !attempt.HeaderCaptured && LastDiagnosticInvocationCompletion is { HeaderCapturedBeforeReset: true })
        {
            return;
        }
        NativeBattleGraphicsState graphics = NativeGraphics ?? throw new InvalidDataException("Focal completion lost its actual graphical owner.");
        bool frameEnd = executingCertificate is not null;
        bool exactPacket = !frameEnd && attempt.PacketSlot >= 0 && graphics.Descriptors.Queue.Length == 1 && graphics.Descriptors.Queue[0] == attempt.PacketSlot && graphics.Descriptors.Packets[attempt.PacketSlot] is { Command: 10 } packet && packet.Serial == attempt.PacketSerial && packet.Argument == attempt.Callback;
        NativeBattleDescriptorPacket? retiredPacket = frameEnd && attempt.PacketSlot >= 0 ? graphics.Descriptors.Packets[attempt.PacketSlot] : null;
        bool removed = frameEnd && retiredWitness is not null && ReferenceEquals(retiredPacket, retiredWitness) && executingCertificate!.CompletionPacket is { } original && retiredPacket is not null && !graphics.Descriptors.Queue.Contains(attempt.PacketSlot) && retiredPacket.UnknownByteMask == original.UnknownByteMask && retiredPacket.Bytes.AsSpan().SequenceEqual(original.Bytes.SetItem(1, 0).AsSpan());
        BattleDiagnosticInvocationOwners owners = CaptureDiagnosticInvocationOwnerState(graphics);
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
        if (frameEnd ? !removed : !exactPacket)
        {
            blockers.Add(frameEnd ? "completion-packet-not-retired-or-slot-reused" : "executing-completion-packet-mismatch");
        }
        if (frameEnd && owners.DescriptorPackets != 0 || owners.NonPumpDescriptorTasks != 0 || owners.PendingContacts != 0)
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
        LastDiagnosticInvocationCompletion = new(DiagnosticLifecycleId, NativeHostCallbacks ?? throw new NotSupportedException("Focal completion requires its actual native callback."), Frame, Stopwatch.GetTimestamp(), attempt.Dispatch, attempt.Header, attempt.Packet, attempt.Callback, attempt.ActorIndex, attempt.PacketSlot, attempt.PacketSerial, attempt.Callback, completed, acknowledged, attempt.HeaderCaptured, frameEnd ? executingCertificate!.CurrentCompletionPacketGuaranteedRemoval : exactPacket, owners, blockers.ToImmutable())
        {
            Boundary = frameEnd ? BattleDiagnosticInvocationRelease.GraphicsFrameBoundary : BattleDiagnosticInvocationRelease.ExecutingBoundary,
            PacketRemoved = removed,
            GraphicsCallback = graphics.Callback,
            RemovedCompletionPacket = retiredPacket,
            ExecutingCompletion = executingCertificate
        };
        if (frameEnd && !LastDiagnosticInvocationCompletion.Released)
        {
            BattleDiagnosticInvocationRelease? scoped = TrySealDiagnosticInvocationAftercare(attempt, LastDiagnosticInvocationCompletion, graphics);
            if (scoped is not null)
            {
                LastDiagnosticInvocationCompletion = scoped;
            }
        }
        if (LastDiagnosticInvocationCompletion.Released)
        {
            DiagnosticInvocationReleased?.Invoke(LastDiagnosticInvocationCompletion);
        }
        else if (!frameEnd && completed && acknowledged && attempt.HeaderCaptured && exactPacket)
        {
            diagnosticPendingFrameAttempt = attempt;
            diagnosticPendingFrameCertificate = LastDiagnosticInvocationCompletion;
            diagnosticPendingRetiredPacket = null;
        }
        if (frameEnd && !LastDiagnosticInvocationCompletion.Released)
        {
            CaptureDiagnosticInvocationPendingOwners(graphics);
        }
    }
    private void ObserveDiagnosticInvocationPacketRemoved(NativeBattleDescriptorState state, int slot, NativeBattleDescriptorPacket original)
    {
        ObserveDiagnosticInvocationAftercarePacketRemoved(state, slot, original);
        if (diagnosticPendingFrameAttempt is not { } attempt || slot != attempt.PacketSlot || !ReferenceEquals(original, attempt.Packet))
        {
            return;
        }
        if (diagnosticPendingRetiredPacket is not null)
        {
            diagnosticPendingFrameAttempt = null;
            diagnosticPendingFrameCertificate = null;
            diagnosticPendingRetiredPacket = null;
            LastDiagnosticInvocationPendingOwners = new { Reason = "duplicate-completion-packet-removal" };
            return;
        }
        diagnosticPendingRetiredPacket = state.Packets[slot];
    }

    private void ObserveDiagnosticInvocationGraphicsFrameEnd()
    {
        DiagnosticInvocationCompletion? attempt = diagnosticPendingFrameAttempt;
        BattleDiagnosticInvocationRelease? certificate = diagnosticPendingFrameCertificate;
        NativeBattleDescriptorPacket? retiredWitness = diagnosticPendingRetiredPacket;
        diagnosticPendingFrameAttempt = null;
        diagnosticPendingFrameCertificate = null;
        diagnosticPendingRetiredPacket = null;
        if (attempt is null || certificate is null)
        {
            return;
        }
        if (attempt.LifecycleId != DiagnosticLifecycleId || attempt.SimulationFrame != Frame || attempt.HostCallback != NativeHostCallbacks || attempt.GraphicsCallback + 1 != NativeGraphics?.Callback || attempt.DispatchSerial != diagnosticDispatchSerial || attempt.Dispatch.Serial != diagnosticInvocationDispatch?.Serial || LastDiagnosticInvocationCompletion is { Released: true })
        {
            LastDiagnosticInvocationPendingOwners = new { Reason = "same-native-graphics-frame-boundary-expired", ExpectedCallback = attempt.HostCallback, ActualCallback = NativeHostCallbacks, ExpectedFrame = attempt.SimulationFrame, ActualFrame = Frame };
            return;
        }
        ObserveDiagnosticInvocationCompletion(attempt, true, true, certificate, retiredWitness);
    }

    private void CaptureDiagnosticInvocationPendingOwners(NativeBattleGraphicsState graphics)
    {
        List<object> auxiliary = [];
        int? next = graphics.Auxiliary.Head;
        for (int visited = 0; next is int slot && visited < Math.Min(graphics.Auxiliary.Slots.Length, 64); visited++)
        {
            NativeBattleAuxiliaryTask task = graphics.Auxiliary.Slots[slot];
            auxiliary.Add(new { Slot = slot, task.Kind, task.Flags, task.State, task.Next, TextureAnimation = ObserveDiagnosticTextureAnimation(task, graphics) });
            next = task.Next;
        }
        LastDiagnosticInvocationPendingOwners = new
        {
            Callback = NativeHostCallbacks,
            SimulationFrame = Frame,
            GraphicsCallback = graphics.Callback,
            ActiveAction = ActiveAction is { } active ? new { active.ActorId, active.TargetId, active.Ability, active.ActionId, active.NativeHeaderIndex, active.NativeQueuedCommand } : null,
            QueuedPresentationCount = actions.Count,
            QueuedPresentations = actions.Take(64).Select(command => new { command.ActorId, command.TargetId, command.Ability, command.ActionId, command.NativeHeaderIndex, command.NativeQueuedCommand, command.NativeCallback }).ToArray(),
            BoundCommands = nativeCommandBanks.Banks.SelectMany((bank, bankIndex) => bank.LogicalCommands.Select((command, packetIndex) => new { command, bankIndex, packetIndex })).Where(row => row.command is not null).Take(66).Select(row => new { Bank = row.bankIndex, Slot = row.packetIndex / 2, Subcommand = row.packetIndex % 2, row.command!.ActorId, row.command!.TargetId, row.command!.Ability, row.command!.ActionId, row.command!.NativeHeaderIndex, row.command!.NativeQueuedCommand, row.command!.NativeCallback }).ToArray(),
            DispatchedCommand = nativeDispatchCommandLocation,
            Auxiliary = auxiliary.ToArray(),
            AuxiliaryTruncated = next is not null
        };
    }

    private static object? ObserveDiagnosticTextureAnimation(NativeBattleAuxiliaryTask task, NativeBattleGraphicsState graphics)
    {
        if (task.Kind != NativeBattleAuxiliaryKind.TextureAnimation5057D0)
        {
            return null;
        }
        bool known = !task.Payload.IsDefault && task.Payload.Length >= 12 && (task.UnknownByteMask & 0x20F) == 0;
        uint? pointer = known ? System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan()) : null;
        byte? sequence = known ? task.Payload[9] : null;
        uint relative = pointer is uint observed ? unchecked(observed - 0x1D972C0u) : uint.MaxValue;
        int? index = relative % 156 == 0 && relative / 156 < 7 ? (int)(relative / 156) : null;
        NativeBattleGraphicsModel? model = index is int selected && !graphics.Models.IsDefault && selected < graphics.Models.Length ? graphics.Models[selected] : null;
        NativeBattleAuxiliaryProgram? program = task.Program;
        int? sourceLength = program is not null && !program.Source.IsDefault ? program.Source.Length : null;
        byte? deadline = program is not null && !program.Source.IsDefault && (uint)program.Offset < program.Source.Length ? program.Source[program.Offset] : null;
        return new
        {
            Payload = task.Payload.IsDefault ? null : task.Payload.Take(64).ToArray(),
            PayloadLength = task.Payload.IsDefault ? (int?)null : task.Payload.Length,
            PayloadTruncated = !task.Payload.IsDefault && task.Payload.Length > 64,
            task.UnknownByteMask,
            PointerAndSequenceKnown = known,
            ModelPointer = pointer,
            CapturedSequence = sequence,
            Program = program is null ? null : new { program.Offset, SourceLength = sourceLength, ActualByteAtOffset = deadline },
            Model = model is null ? null : new { model.Index, model.Flags0, model.ResourceFlags44, model.Sequence }
        };
    }

    private BattleDiagnosticInvocationOwners CaptureDiagnosticInvocationOwnerState(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Focal completion lost its returned-task owner.");
        int? files = NativeFileIo?.ActiveCount;
        return new(graphics.Descriptors.Queue.Length, graphics.Descriptors.Tasks.Count(task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0), graphics.PendingContacts.Length, graphics.Effects.PublishedCount, graphics.Effects.Head is not null, graphics.Auxiliary.Head is not null, graphics.ModuleEffects.PublishedCount, graphics.ModuleEffects.Head is not null, files, memory.ReturnedTaskPointer1D96AAC, memory.DetachedReturnedTask is not null, NativeCameraTaskMask, NativeOriginalSoundBusy(), graphics.Models.Any(model => (model.Flags0 & 2) != 0 && (model.ResourceMagicId is not null || model.ActiveStagedResourceRow100 is not null)), ActiveAction is not null || actions.Count != 0 || nativeCommandBanks.BoundCommands.Any(), nativeTimedAction is not null || nativeActionCommitDepth != 0 || HasNativePendingReactions || pendingSlotsDeal is not null || nativeShotProcesses.Count != 0 || nativeDuelProcesses.Count != 0 || nativeRenzokukenProcesses.Count != 0)
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
    }

}
