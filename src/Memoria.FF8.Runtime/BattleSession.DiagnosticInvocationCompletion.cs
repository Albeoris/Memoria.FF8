using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticInvocationPool(uint DescriptorAddress, uint BankAddress, ushort Stride, int ActiveRows, bool Head, bool Tail);
public sealed record BattleDiagnosticVictoryCombatant(string Id, int NativeIndex, bool IsPlayer, uint NativeFlags, ushort SavedStatus, uint BattleStatus, int CurrentHp);
public sealed record BattleDiagnosticNaturalVictory(Guid LifecycleId, long Callback, long SimulationFrame, BattleOutcome Candidate, bool NativeReady, bool PendingOutcome, bool ExceptionalOutcome, ImmutableArray<BattleDiagnosticVictoryCombatant> Combatants)
{
    public bool IsNaturalVictory => LifecycleId != Guid.Empty && Callback >= 0 && SimulationFrame >= 0 && Candidate == BattleOutcome.Victory && NativeReady && !PendingOutcome && !ExceptionalOutcome && !Combatants.IsDefaultOrEmpty && Combatants.Length <= 11 && Combatants.All(actor => actor is not null && !string.IsNullOrWhiteSpace(actor.Id) && actor.NativeIndex is >= -1 and < 7 && (actor.IsPlayer ? actor.NativeIndex < 3 : actor.NativeIndex is -1 or >= 3)) && Combatants.Count(actor => actor.IsPlayer) <= 3 && Combatants.Count(actor => !actor.IsPlayer) <= 8 && Combatants.Select(actor => actor.Id).Distinct(StringComparer.Ordinal).Count() == Combatants.Length && Combatants.Where(actor => actor.NativeIndex >= 0).Select(actor => actor.NativeIndex).Distinct().Count() == Combatants.Count(actor => actor.NativeIndex >= 0) && Combatants.Any(actor => actor.IsPlayer && actor.NativeIndex is >= 0 and < 3 && (actor.NativeFlags & 1) != 0 && (actor.SavedStatus & 5) == 0 && actor.CurrentHp > 0) && Combatants.Any(actor => !actor.IsPlayer) && !Combatants.Any(actor => !actor.IsPlayer && actor.NativeIndex is >= 3 and < 7 && (actor.SavedStatus & 5) == 0);
}
public sealed record BattleDiagnosticTerminalTextureRelease([property: JsonRequired] BattleDiagnosticTextureQuiescence Textures, [property: JsonRequired] BattleDiagnosticTerminalHoldState Hold, [property: JsonRequired] ImmutableArray<BattleDiagnosticCommandBankClosure> CommandBanks)
{
    public BattleDiagnosticNaturalVictory? NaturalVictory { get; init; }
}
public sealed record BattleDiagnosticInvocationOwners(int DescriptorPackets, int NonPumpDescriptorTasks, int PendingContacts, int Effects, bool EffectHead, bool AuxiliaryHead, int Modules, bool ModuleHead, int? Files, uint? ReturnedTaskPointer, bool DetachedReturnedTask, byte CameraMask, bool SoundBusy, bool ReplacementResource, bool ActionOrCommand, bool TimedOrReaction)
{
    public int ActionHeaders { get; init; }
    public int ContactRows { get; init; }
    public int? RetainedActiveHeader { get; init; }
    public int? RetainedActiveContact { get; init; }
    public bool ObservedOriginalEffect { get; init; }
    public uint? SelectedReturnedDescriptor { get; init; }
    public bool HeaderGraphicsBusy { get; init; }
    public ImmutableArray<BattleDiagnosticInvocationPool> OriginalPools { get; init; }
}
public sealed record BattleDiagnosticInvocationRelease(Guid LifecycleId, long Callback, long SimulationFrame, long Timestamp, BattleDiagnosticEffectDispatch Dispatch, NativeBattleRetainedActionHeaderSlot Header, NativeBattleDescriptorPacket? CompletionPacket, uint CompletionCallback, byte EventActorIndex, int CompletionPacketSlot, byte CompletionPacketSerial, uint CompletionPacketArgument, bool NativeEventCompleted, bool PresentationAcknowledged, bool HeaderCapturedBeforeReset, bool CurrentCompletionPacketGuaranteedRemoval, BattleDiagnosticInvocationOwners Owners, ImmutableArray<string> Blockers)
{
    public const string ExecutingBoundary = "executing-completion";
    public const string GraphicsFrameBoundary = "completed-graphics-frame";
    public const string AftercareBoundary = "completed-graphics-frame-aftercare";
    public const string TerminalTextureBoundary = "completed-graphics-frame-terminal-textures";
    public BattleDiagnosticInvocationAftercarePlan? Aftercare { get; init; }
    public BattleDiagnosticTerminalTextureRelease? TerminalTextures { get; init; }
    public string Boundary { get; init; } = ExecutingBoundary;
    public bool PacketRemoved { get; init; }
    public long? GraphicsCallback { get; init; }
    public NativeBattleDescriptorPacket? RemovedCompletionPacket { get; init; }
    public BattleDiagnosticInvocationRelease? ExecutingCompletion { get; init; }
    public bool Released => NativeEventCompleted && PresentationAcknowledged && HeaderCapturedBeforeReset && CurrentCompletionPacketGuaranteedRemoval && (Blockers.IsEmpty || Aftercare is not null || TerminalTextures is not null);
}

public sealed partial class BattleSession
{
    private sealed record DiagnosticInvocationPolicy(ushort Effect, string ActorId, byte? Command, int ActionId);
    private sealed record DiagnosticInvocationCompletion(BattleDiagnosticEffectDispatch Dispatch, NativeBattleRetainedActionHeaderSlot Header, NativeBattleDescriptorPacket? Packet, byte ActorIndex, uint Callback, int PacketSlot, byte PacketSerial, bool HeaderCaptured, Guid LifecycleId, long SimulationFrame, long HostCallback, long GraphicsCallback, long DispatchSerial);
    public event Action<BattleDiagnosticInvocationRelease>? DiagnosticInvocationReleased;

    private DiagnosticInvocationPolicy? diagnosticInvocationPolicy;
    private BattleDiagnosticEffectDispatch? diagnosticInvocationDispatch;
    private DiagnosticInvocationCompletion? diagnosticPendingFrameAttempt;
    private BattleDiagnosticInvocationRelease? diagnosticPendingFrameCertificate;
    private NativeBattleDescriptorPacket? diagnosticPendingRetiredPacket;
    private (int Slot, NativeBattleDescriptorPacket Original, NativeBattleDescriptorPacket Retired)? diagnosticRetiredCompletion;
    private (NativeBattlePresentationCompletionRequested Receipt, DiagnosticInvocationCompletion Attempt, NativeBattleDescriptorPacket Retired)? diagnosticAwaitingCompletionAck;
    public object? LastDiagnosticInvocationPendingOwners { get; private set; }
    public BattleDiagnosticInvocationRelease? LastDiagnosticInvocationCompletion { get; private set; }

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

    private void ObservePresentationDescriptorRemoved(NativeBattleDescriptorState state, int slot, NativeBattleDescriptorPacket original)
    {
        ObserveDiagnosticInvocationAftercarePacketRemoved(state, slot, original);
        if (diagnosticInvocationDispatch is null || LastDiagnosticInvocationCompletion is { Released: true } ||
            expandedHostedEvent is not { CompletionToken: not null } pending || original.Command != 10 || original.Argument != pending.Callback)
        {
            return;
        }
        if (diagnosticRetiredCompletion is not null)
        {
            throw new InvalidDataException("Diagnostic completion observed more than one matching descriptor retirement in the same frame.");
        }
        diagnosticRetiredCompletion = (slot, original, state.Packets[slot]);
    }

    private void ObserveDiagnosticHostedCompletionBeforeAck(BattlePresentationProcess process, NativeBattlePresentationCompletionRequested receipt)
    {
        if (diagnosticRetiredCompletion is not { } retired || diagnosticInvocationDispatch is not { NativeHeaderIndex: int header, NativeHeaderGeneration: > 0, NativeActorIndex: byte actor } dispatch ||
            receipt.ActorId != dispatch.ActorId || retired.Original.Argument != receipt.Callback || process.ActionPackets.ActionCount <= header)
        {
            return;
        }
        NativeBattleActionPacketsState packets = process.ActionPackets;
        NativeBattleActionHeaderSlot source = packets.ReadHeader(new BattleActionHeaderReference(header));
        bool captured = source.Source == receipt.Owner && source.Command == dispatch.NativeCommand && source.Action == dispatch.ActionId &&
            source.Effect == dispatch.RequestedEffect && packets.ModelAliases.TryEncodeExact(source.Source, out byte operand) && operand == actor;
        DiagnosticInvocationCompletion attempt = new(dispatch, packets.ExportRetained().Headers[header], retired.Original, actor, receipt.Callback,
            retired.Slot, retired.Original.Serial, captured, DiagnosticLifecycleId, Frame, NativeHostCallbacks ?? process.Graphics.Callback, process.Graphics.Callback, diagnosticDispatchSerial);
        diagnosticAwaitingCompletionAck = (receipt, attempt, retired.Retired);
        BeginDiagnosticInvocationFocalCompletion(attempt);
    }

    private void ObserveDiagnosticHostedCompletionAcknowledged(BattlePresentationProcess process, NativeBattlePresentationCompletionRequested receipt)
    {
        if (diagnosticAwaitingCompletionAck is not { } observed || !ReferenceEquals(observed.Receipt, receipt))
        {
            return;
        }
        diagnosticAwaitingCompletionAck = null;
        EndDiagnosticInvocationFocalCompletion();
        ObserveDiagnosticInvocationCompletion(observed.Attempt, completed: true, acknowledged: process.IsCompletionAcknowledged(receipt.Token), retiredWitness: observed.Retired);
    }

    public void ConfigureDiagnosticInvocationCompletion(ushort effect, string actorId, byte? command, int actionId)
    {
        if (diagnosticInvocationPolicy is not null || effect == 0 || string.IsNullOrWhiteSpace(actorId) || actionId < 0 || DiagnosticEffectDispatched is null)
        {
            throw new InvalidOperationException("Focal completion requires one explicit live diagnostic observer and invocation.");
        }
        diagnosticInvocationPolicy = new(effect, actorId, command, actionId);
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
        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("Focal completion requires the current presentation process.");
        NativeBattleActionPacketsState packets = process.ActionPackets;
        NativeBattleActionHeaderSlot physicalHeader = packets.ReadHeader(new BattleActionHeaderReference(header));
        bool captured = packets.ModelAliases.TryEncodeExact(physicalHeader.Source, out byte sourceAlias) && sourceAlias == actor &&
            physicalHeader.Command == dispatch.NativeCommand && physicalHeader.Action == dispatch.ActionId && physicalHeader.Effect == dispatch.RequestedEffect &&
            dispatch.Serial <= diagnosticDispatchSerial && diagnosticHeaderGenerations is { } generations && generations.TryGetValue(header, out long actual) &&
            actual == generation && generation > 0 && nativeActionScratch.EventActorB0F == actor && dispatch.Serial > 0;
        int slot = graphics.Descriptors.Queue.IsEmpty ? -1 : graphics.Descriptors.Queue[0];
        byte serial = slot < 0 ? (byte)0 : graphics.Descriptors.Packets[slot].Serial;
        return new(dispatch, packets.ExportRetained().Headers[header], slot < 0 ? null : graphics.Descriptors.Packets[slot], actor, callback, slot, serial, captured, DiagnosticLifecycleId, Frame, NativeHostCallbacks ?? throw new NotSupportedException("Focal completion requires its actual native callback."), graphics.Callback, diagnosticDispatchSerial);
    }

    private void ObserveDiagnosticInvocationCompletion(DiagnosticInvocationCompletion? attempt, bool completed, bool acknowledged, BattleDiagnosticInvocationRelease? executingCertificate = null, NativeBattleDescriptorPacket? retiredWitness = null)
    {
        if (attempt is null || !attempt.HeaderCaptured && LastDiagnosticInvocationCompletion is { HeaderCapturedBeforeReset: true })
        {
            return;
        }
        NativeBattleGraphicsState graphics = (Presentation ?? throw new InvalidOperationException("Focal completion requires the current presentation process.")).Graphics;
        bool frameEnd = executingCertificate is not null;
        bool exactPacket = !frameEnd && attempt.PacketSlot >= 0 && attempt.Packet is { Command: 10 } packet && packet.Serial == attempt.PacketSerial && packet.Argument == attempt.Callback &&
            (retiredWitness is not null ? ReferenceEquals(graphics.Descriptors.Packets[attempt.PacketSlot], retiredWitness) :
                graphics.Descriptors.Queue.Length == 1 && graphics.Descriptors.Queue[0] == attempt.PacketSlot && ReferenceEquals(graphics.Descriptors.Packets[attempt.PacketSlot], packet));
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
            BattleDiagnosticInvocationRelease? scoped = TrySealDiagnosticTerminalTextures(LastDiagnosticInvocationCompletion, graphics) ?? TrySealDiagnosticInvocationAftercare(attempt, LastDiagnosticInvocationCompletion, graphics);
            if (scoped is not null)
            {
                LastDiagnosticInvocationCompletion = scoped;
            }
            if (LastDiagnosticTerminalTextureAttempt is { Result: "pending" } textureAttempt)
            {
                LastDiagnosticTerminalTextureAttempt = textureAttempt with { Result = scoped?.TerminalTextures is not null ? "sealed" : scoped?.Aftercare is not null ? "deferred-aftercare" : "unsupported" };
            }
        }
        if (LastDiagnosticInvocationCompletion.Released && (frameEnd || retiredWitness is null))
        {
            DiagnosticInvocationReleased?.Invoke(LastDiagnosticInvocationCompletion);
        }
        else if (!frameEnd && completed && acknowledged && attempt.HeaderCaptured && exactPacket)
        {
            diagnosticPendingFrameAttempt = attempt;
            diagnosticPendingFrameCertificate = LastDiagnosticInvocationCompletion;
            diagnosticPendingRetiredPacket = retiredWitness;
        }
        if (frameEnd && !LastDiagnosticInvocationCompletion.Released)
        {
            CaptureDiagnosticInvocationPendingOwners(graphics);
        }
    }

    private void ObservePresentationFrameEnded(BattlePresentationProcess process)
    {
        diagnosticRetiredCompletion = null;
        ObserveDiagnosticInvocationGraphicsFrameEnd(process);
        ObserveDiagnosticInvocationAftercareFrameEnd();
    }

    private void ObserveDiagnosticInvocationGraphicsFrameEnd(BattlePresentationProcess process)
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
        if (attempt.LifecycleId != DiagnosticLifecycleId || attempt.SimulationFrame != Frame || attempt.HostCallback != NativeHostCallbacks || attempt.GraphicsCallback != process.Graphics.Callback || attempt.DispatchSerial != diagnosticDispatchSerial || attempt.Dispatch.Serial != diagnosticInvocationDispatch?.Serial ||
            LastDiagnosticInvocationCompletion is { Released: true, Boundary: not BattleDiagnosticInvocationRelease.ExecutingBoundary })
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
            ActiveAction = ActiveAction is { } active ? new { active.ActorId, active.TargetId, active.Ability, active.ActionId, active.NativeHeader, active.NativeQueuedCommand } : null,
            QueuedPresentationCount = actions.Count,
            QueuedPresentations = actions.Take(64).Select(command => new { command.ActorId, command.TargetId, command.Ability, command.ActionId, command.NativeHeader, command.NativeQueuedCommand, command.NativeCallback }).ToArray(),
            BoundCommands = nativeCommandBanks.Banks.SelectMany((bank, bankIndex) => bank.LogicalCommands.Select((command, packetIndex) => new { command, bankIndex, packetIndex })).Where(row => row.command is not null).Take(66).Select(row => new { Bank = row.bankIndex, Slot = row.packetIndex / 2, Subcommand = row.packetIndex % 2, row.command!.ActorId, row.command!.TargetId, row.command!.Ability, row.command!.ActionId, NativeHeaderIndex = OriginalDiagnosticHeaderIndex(row.command!.NativeHeader), row.command!.NativeQueuedCommand, row.command!.NativeCallback }).ToArray(),
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
        (int? index, NativeBattleGraphicsModel? model) = ResolveDiagnosticTextureModel(task, graphics, pointer);
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
            Model = model is null ? null : new { Index = index, model.Flags0, model.ResourceFlags44, model.Sequence }
        };
    }

    private static (int? Index, NativeBattleGraphicsModel? Model) ResolveDiagnosticTextureModel(NativeBattleAuxiliaryTask task, NativeBattleGraphicsState graphics, uint? pointer)
    {
        if (task.ModelOwner is not { } owner || !graphics.ActorModels.Contains(owner))
        {
            return (null, null);
        }
        int? index = pointer is uint original && NativeBattleGraphicsModelAddress.TryDecodeOriginalCell(original, out NativeBattleModelSlot slot) &&
            graphics.NativeBindings.FindOriginalOwner(slot, graphics.ActorModels) == owner ? slot.Offset : null;
        return (index, graphics.ActorModels.Get(owner));
    }

    private BattleDiagnosticInvocationOwners CaptureDiagnosticInvocationOwnerState(NativeBattleGraphicsState graphics)
    {
        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("Invocation observations require the current presentation process.");
        NativeBattleEffectMemory memory = process.Memory;
        NativeBattleActionPacketsState packets = process.ActionPackets;
        int? files = process.NativeFileIo?.ActiveCount;
        return new(graphics.Descriptors.Queue.Length, graphics.Descriptors.Tasks.Count(task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0), graphics.PendingContacts.Length, graphics.Effects.PublishedCount, graphics.Effects.Head is not null, graphics.Auxiliary.Head is not null, graphics.ModuleEffects.PublishedCount, graphics.ModuleEffects.Head is not null, files, memory.ReturnedTaskPointer1D96AAC, memory.DetachedReturnedTask is not null, process.NativeCameraTaskMask, process.ObserveNativeSoundBusy(), graphics.ActorModels.Entries.Select(entry => entry.State).Any(model => (model.Flags0 & 2) != 0 && (model.ResourceMagicId is not null || model.ActiveStagedResourceRow100 is not null)), ActiveAction is not null || actions.Count != 0 || nativeCommandBanks.BoundCommands.Any(), nativeTimedAction is not null || nativeActionCommitDepth != 0 || HasNativePendingReactions || pendingSlotsDeal is not null || nativeShotProcesses.Count != 0 || nativeDuelProcesses.Count != 0 || nativeRenzokukenProcesses.Count != 0)
        {
            ActionHeaders = packets.ActionCount,
            ContactRows = packets.ContactCount,
            RetainedActiveHeader = packets.RetainedStorage.ActiveHeaderIndex,
            RetainedActiveContact = packets.RetainedStorage.ActiveContactIndex,
            ObservedOriginalEffect = graphics.OriginalEffect is not null,
            SelectedReturnedDescriptor = graphics.OriginalEffect?.ReturnedDescriptor,
            HeaderGraphicsBusy = (graphics.Flags & 0x10) != 0,
            OriginalPools = graphics.OriginalEffect is { } selected ? selected.Pools.Select(pool => new BattleDiagnosticInvocationPool(pool.DescriptorAddress, pool.BankAddress, pool.Stride, pool.Slots.Count(task => (task.Flags & 1) != 0), pool.Head is not null, pool.Tail is not null)).ToImmutableArray() : []
        };
    }
}
