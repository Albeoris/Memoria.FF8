using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticInvocationPool(uint DescriptorAddress, uint BankAddress, ushort Stride, int ActiveRows, bool Head, bool Tail);
public sealed record BattleDiagnosticCompletionGate(string Stage, long Callback, long GraphicsCallback, long DispatchSerial, long? HostedCompletionToken, string? HostedActor, long? ReceiptToken, string? ReceiptActor, string? ReceiptOwner, int? RetiredSlot, ushort? PacketCommand, uint? PacketArgument, int? HeaderOrdinal, long? HeaderGeneration, long? ExpectedHeaderGeneration, string? HeaderSource, byte? HeaderActor, byte? ExpectedHeaderActor, byte? HeaderCommand, int? HeaderAction, ushort? HeaderEffect);
public sealed record BattleDiagnosticVictoryCombatant(string Id, int? NativeIndex, bool IsPlayer, uint NativeFlags, ushort SavedStatus, uint BattleStatus, int CurrentHp)
{
    public BattleRosterSlot Slot { get; init; }
    public bool BattleCapable { get; init; }
}
public sealed record BattleDiagnosticNaturalVictory(Guid LifecycleId, long Callback, long SimulationFrame, BattleOutcome Candidate, bool NativeReady, bool PendingOutcome, bool ExceptionalOutcome, ImmutableArray<BattleDiagnosticVictoryCombatant> Combatants)
{
    public bool IsNaturalVictory => LifecycleId != Guid.Empty && Callback >= 0 && SimulationFrame >= 0 && Candidate == BattleOutcome.Victory &&
        NativeReady && !PendingOutcome && !ExceptionalOutcome && !Combatants.IsDefaultOrEmpty &&
        Combatants.All(actor => actor is not null && !string.IsNullOrWhiteSpace(actor.Id) && actor.Slot.Side == (actor.IsPlayer ? BattleSide.Allies : BattleSide.Enemies) &&
            (actor.NativeIndex is null || actor.NativeIndex is >= 0 and < NativeBattleSlots.Count && NativeBattleSlots.Decode(actor.NativeIndex.Value).Side == actor.Slot.Side) &&
            actor.BattleCapable == ((actor.SavedStatus & 5) == 0 && (actor.BattleStatus & 0x10000) == 0)) &&
        Combatants.Select(actor => actor.Id).Distinct(StringComparer.Ordinal).Count() == Combatants.Length &&
        Combatants.Select(actor => actor.Slot).Distinct().Count() == Combatants.Length &&
        Combatants.Where(actor => actor.NativeIndex.HasValue).Select(actor => actor.NativeIndex).Distinct().Count() == Combatants.Count(actor => actor.NativeIndex.HasValue) &&
        Combatants.Any(actor => actor.IsPlayer && actor.BattleCapable) && !Combatants.Any(actor => !actor.IsPlayer && actor.BattleCapable);
}
public sealed record BattleDiagnosticTerminalTextureRelease([property: JsonRequired] BattleDiagnosticTextureQuiescence Textures, [property: JsonRequired] BattleDiagnosticTerminalHoldState Hold)
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
public sealed record BattleDiagnosticInvocationRelease(Guid LifecycleId, long Callback, long SimulationFrame, long Timestamp, BattleDiagnosticEffectDispatch Dispatch, NativeBattleRetainedActionHeaderSlot? Header, NativeBattleDescriptorPacket? CompletionPacket, uint CompletionCallback, byte? EventActorIndex, int CompletionPacketSlot, byte CompletionPacketSerial, uint CompletionPacketArgument, bool NativeEventCompleted, bool PresentationAcknowledged, bool HeaderCapturedBeforeReset, bool CurrentCompletionPacketGuaranteedRemoval, BattleDiagnosticInvocationOwners Owners, ImmutableArray<string> Blockers)
{
    public const string ExecutingBoundary = "executing-completion";
    public const string GraphicsFrameBoundary = "completed-graphics-frame";
    public const string TerminalTextureBoundary = "completed-graphics-frame-terminal-textures";
    public BattleDiagnosticTerminalTextureRelease? TerminalTextures { get; init; }
    public string Boundary { get; init; } = ExecutingBoundary;
    public BattleActionHeaderReference? HeaderReference { get; init; }
    public BattleRosterSlot? EventSlot { get; init; }
    public long CompletionToken { get; init; }
    public bool PacketRemoved { get; init; }
    public long? GraphicsCallback { get; init; }
    public NativeBattleDescriptorPacket? RemovedCompletionPacket { get; init; }
    public BattleDiagnosticInvocationRelease? ExecutingCompletion { get; init; }
    public ImmutableArray<BattleDiagnosticInvocationPendingCallback> PendingFollowups { get; init; } = [];
    public ImmutableArray<BattleDiagnosticInvocationPendingTexture> PendingTextures { get; init; } = [];
    public bool Released => NativeEventCompleted && PresentationAcknowledged && HeaderCapturedBeforeReset && CurrentCompletionPacketGuaranteedRemoval && (Blockers.IsEmpty || TerminalTextures is not null);
}

public sealed partial class BattleSession
{
    private sealed record DiagnosticInvocationPolicy(ushort Effect, string ActorId, byte? Command, int ActionId);
    private sealed record DiagnosticInvocationCompletion(BattleDiagnosticEffectDispatch Dispatch, BattleActionHeaderReference HeaderReference, NativeBattleRetainedActionHeaderSlot? Header, NativeBattleDescriptorPacket? Packet, byte? ActorIndex, BattleRosterSlot EventSlot, long CompletionToken, uint Callback, int PacketSlot, byte PacketSerial, bool HeaderCaptured, Guid LifecycleId, long SimulationFrame, long HostCallback, long GraphicsCallback, long DispatchSerial, long Timestamp, BattleDiagnosticInvocationOwners ExecutingOwners);
    public event Action<BattleDiagnosticInvocationRelease>? DiagnosticInvocationReleased;
    public event Action<BattleDiagnosticCompletionGate>? DiagnosticCompletionGateObserved;
    private bool diagnosticCompletionGatesEnabled;
    private byte diagnosticCompletionGatesRecorded;

    private DiagnosticInvocationPolicy? diagnosticInvocationPolicy;
    private BattleDiagnosticEffectDispatch? diagnosticInvocationDispatch;
    private DiagnosticInvocationCompletion? diagnosticPendingFrameAttempt;
    private BattleDiagnosticInvocationRelease? diagnosticPendingFrameCertificate;
    private NativeBattleDescriptorPacket? diagnosticPendingRetiredPacket;
    private (int Slot, NativeBattleDescriptorPacket Original, NativeBattleDescriptorPacket Retired, long GraphicsCallback, long Timestamp, BattleDiagnosticInvocationOwners Owners)? diagnosticRetiredCompletion;
    private (NativeBattlePresentationCompletionRequested Receipt, DiagnosticInvocationCompletion Attempt, NativeBattleDescriptorPacket Retired)? diagnosticAwaitingCompletionAck;
    public object? LastDiagnosticInvocationPendingOwners { get; private set; }
    public BattleDiagnosticInvocationRelease? LastDiagnosticInvocationCompletion { get; private set; }

    public object ObserveDiagnosticInvocationCompletionState()
    {
        return new
        {
            Owners = LastDiagnosticInvocationCompletion?.Owners is { } owners ? owners with { OriginalPools = owners.OriginalPools.IsDefault ? [] : owners.OriginalPools.Take(16).ToImmutableArray() } : null,
            FollowupAmbiguous = diagnosticFollowupAmbiguous,
            FollowupRetirementCaptured = diagnosticFollowupRetirement is not null,
            FollowupReleased = LastDiagnosticInvocationFollowupRelease is not null,
            CallbackCount = diagnosticFollowupCallbacks?.Count ?? 0,
            Callbacks = diagnosticFollowupCallbacks?.Take(16).Select(pair => new { Actor = pair.Key.Actor is BattlePartyActorReference party ? party.ActorId : null, pair.Key.Stage, pair.Value.Queued, pair.Value.Started, pair.Value.Completed, pair.Value.NoOp }).ToArray(),
            TextureCount = diagnosticFollowupTextures?.Count ?? 0,
            TextureReleaseCount = diagnosticFollowupTextures?.Values.Count(texture => texture.Release is not null) ?? 0
        };
    }

    private void ObserveDiagnosticInvocationDispatch(BattleDiagnosticEffectDispatch dispatch)
    {
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

    private void ObservePresentationDescriptorRemoved(NativeBattleGraphicsState graphics, NativeBattleDescriptorState state, int slot, NativeBattleDescriptorPacket original)
    {
        if (original.Command == 10)
        {
            ObserveDiagnosticCompletionGate(1, "descriptor-retired", Presentation, null, slot, original);
        }
        if (diagnosticInvocationDispatch is null || LastDiagnosticInvocationCompletion is { Released: true } ||
            expandedHostedEvent is not { CompletionToken: not null } pending || original.Command != 10 || original.Argument != pending.Callback)
        {
            return;
        }
        if (diagnosticRetiredCompletion is not null)
        {
            throw new InvalidDataException("Diagnostic completion observed more than one matching descriptor retirement in the same frame.");
        }
        diagnosticRetiredCompletion = (slot, original, state.Packets[slot], graphics.Callback, Stopwatch.GetTimestamp(), CaptureDiagnosticInvocationOwnerState(graphics));
    }

    private void ObserveDiagnosticHostedCompletionBeforeAck(BattlePresentationProcess process, NativeBattlePresentationCompletionRequested receipt)
    {
        ObserveDiagnosticCompletionGate(2, "before-completion-ack", process, receipt, diagnosticRetiredCompletion?.Slot, diagnosticRetiredCompletion?.Original);
        if (diagnosticRetiredCompletion is not { } retired ||
            diagnosticInvocationDispatch is not { HeaderReference: { } reference, NativeHeaderGeneration: > 0, ActorSlot: { } slot } dispatch ||
            receipt.ActorId != dispatch.ActorId || retired.Original.Argument != receipt.Callback || process.ActionPackets.ActionCount <= reference.Ordinal)
        {
            return;
        }
        NativeBattleActionPacketsState packets = process.ActionPackets;
        NativeBattleActionHeaderSlot source = packets.ReadHeader(reference);
        byte? originalActor = packets.ModelAliases.TryEncodeExact(source.Source, out byte operand) ? operand : null;
        bool captured = source.Source == receipt.Owner && source.Command == dispatch.NativeCommand && source.Action == dispatch.ActionId &&
            source.Effect == dispatch.RequestedEffect && process.ReadDiagnosticCounterHeaderGeneration(reference.Ordinal) == dispatch.NativeHeaderGeneration &&
            originalActor == dispatch.NativeActorIndex;
        NativeBattleRetainedActionHeaderSlot? raw = dispatch.NativeHeaderIndex is int header ? packets.ExportRetained().Headers[header] : null;
        DiagnosticInvocationCompletion attempt = new(dispatch, reference, raw, retired.Original, originalActor, slot, receipt.Token, receipt.Callback,
            retired.Slot, retired.Original.Serial, captured, DiagnosticLifecycleId, Frame, NativeHostCallbacks ?? process.Graphics.Callback, retired.GraphicsCallback, diagnosticDispatchSerial, retired.Timestamp, retired.Owners);
        diagnosticAwaitingCompletionAck = (receipt, attempt, retired.Retired);
    }

    private void ObserveDiagnosticHostedCompletionAcknowledged(BattlePresentationProcess process, NativeBattlePresentationCompletionRequested receipt)
    {
        if (diagnosticAwaitingCompletionAck is not { } observed || !ReferenceEquals(observed.Receipt, receipt))
        {
            return;
        }
        diagnosticAwaitingCompletionAck = null;
        ObserveDiagnosticInvocationCompletion(observed.Attempt, completed: true, acknowledged: process.IsCompletionAcknowledged(receipt.Token), retiredWitness: observed.Retired);
    }

    public void ConfigureDiagnosticInvocationCompletion(ushort effect, string actorId, byte? command, int actionId)
    {
        if (diagnosticInvocationPolicy is not null || effect == 0 || string.IsNullOrWhiteSpace(actorId) || actionId < 0 || DiagnosticEffectDispatched is null)
        {
            throw new InvalidOperationException("Focal completion requires one explicit live diagnostic observer and invocation.");
        }
        diagnosticInvocationPolicy = new(effect, actorId, command, actionId);
        diagnosticCompletionGatesEnabled = Environment.GetEnvironmentVariable("MEMORIA_DIAGNOSTIC_COMPLETION_GATES") == "1";
    }

    private void ObserveDiagnosticCompletionGate(byte flag, string stage, BattlePresentationProcess? process, NativeBattlePresentationCompletionRequested? receipt, int? retiredSlot, NativeBattleDescriptorPacket? packet)
    {
        if (!diagnosticCompletionGatesEnabled || (diagnosticCompletionGatesRecorded & flag) != 0 || diagnosticInvocationDispatch is not { } dispatch || process is null || DiagnosticCompletionGateObserved is not { } observer)
        {
            return;
        }
        diagnosticCompletionGatesRecorded |= flag;
        BattleActionHeaderReference? reference = dispatch.HeaderReference;
        NativeBattleActionHeaderSlot? header = reference is { } selected && selected.Ordinal >= 0 && selected.Ordinal < process.ActionPackets.ActionCount ? process.ActionPackets.ReadHeader(selected) : null;
        byte? actor = header is not null && process.ActionPackets.ModelAliases.TryEncodeExact(header.Source, out byte operand) ? operand : null;
        uint? argument = packet is not null && (packet.UnknownByteMask & 0xF0) == 0 ? packet.Argument : null;
        observer(new(stage, NativeHostCallbacks ?? Frame, process.Graphics.Callback, dispatch.Serial, expandedHostedEvent?.CompletionToken, expandedHostedEvent?.Command.ActorId, receipt?.Token, receipt?.ActorId, receipt?.Owner.ToString(), retiredSlot, packet?.Command, argument, reference?.Ordinal, header is null ? null : process.ReadDiagnosticCounterHeaderGeneration(reference!.Value.Ordinal), dispatch.NativeHeaderGeneration, header?.Source.ToString(), actor, dispatch.NativeActorIndex, header?.Command, header?.Action, header?.Effect));
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
        BattleDiagnosticInvocationOwners owners = frameEnd ? CaptureDiagnosticInvocationOwnerState(graphics) : attempt.ExecutingOwners;
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
        LastDiagnosticInvocationCompletion = new(DiagnosticLifecycleId, NativeHostCallbacks ?? throw new NotSupportedException("Focal completion requires its actual native callback."), Frame, frameEnd ? Stopwatch.GetTimestamp() : attempt.Timestamp, attempt.Dispatch, attempt.Header, attempt.Packet, attempt.Callback, attempt.ActorIndex, attempt.PacketSlot, attempt.PacketSerial, attempt.Callback, completed, acknowledged, attempt.HeaderCaptured, frameEnd ? executingCertificate!.CurrentCompletionPacketGuaranteedRemoval : exactPacket, owners, blockers.ToImmutable())
        {
            HeaderReference = attempt.HeaderReference,
            EventSlot = attempt.EventSlot,
            CompletionToken = attempt.CompletionToken,
            Boundary = frameEnd ? BattleDiagnosticInvocationRelease.GraphicsFrameBoundary : BattleDiagnosticInvocationRelease.ExecutingBoundary,
            PacketRemoved = removed,
            GraphicsCallback = frameEnd ? graphics.Callback : attempt.GraphicsCallback,
            RemovedCompletionPacket = retiredPacket,
            ExecutingCompletion = executingCertificate
        };
        if (frameEnd && !LastDiagnosticInvocationCompletion.Released)
        {
            BattleDiagnosticInvocationRelease? scoped = TrySealDiagnosticTerminalTextures(LastDiagnosticInvocationCompletion, graphics);
            if (scoped is not null)
            {
                LastDiagnosticInvocationCompletion = scoped;
            }
            if (LastDiagnosticTerminalTextureAttempt is { Result: "pending" } textureAttempt)
            {
                LastDiagnosticTerminalTextureAttempt = textureAttempt with { Result = scoped?.TerminalTextures is not null ? "sealed" : "unsupported" };
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
            ObserveDiagnosticFollowupRetirement(LastDiagnosticInvocationCompletion);
            CaptureDiagnosticInvocationPendingOwners(graphics);
        }
    }

    private void ObservePresentationFrameEnded(BattlePresentationProcess process)
    {
        diagnosticRetiredCompletion = null;
        ObserveDiagnosticInvocationGraphicsFrameEnd(process);
        ObserveDiagnosticFollowupDrain(process);
        ObserveDiagnosticAftercareFrameEnded();
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
        if (attempt.LifecycleId != DiagnosticLifecycleId || attempt.SimulationFrame != Frame || attempt.HostCallback != NativeHostCallbacks || attempt.GraphicsCallback + 1 != process.Graphics.Callback || attempt.DispatchSerial != diagnosticDispatchSerial || attempt.Dispatch.Serial != diagnosticInvocationDispatch?.Serial ||
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
            ActiveAction = ActiveAction is { } active ? new { active.ActorId, active.TargetId, active.Ability, active.ActionId, active.NativeQueuedCommand } : null,
            QueuedPresentationCount = actions.Count,
            QueuedPresentations = actions.Take(64).Select(command => new { command.ActorId, command.TargetId, command.Ability, command.ActionId, command.NativeQueuedCommand, command.ExpandedAiCallback }).ToArray(),
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
        (int? index, NativeBattleGraphicsModel? model) = BattlePresentationProcess.ResolveDiagnosticTextureModel(task, graphics, pointer);
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


    private BattleDiagnosticInvocationOwners CaptureDiagnosticInvocationOwnerState(NativeBattleGraphicsState graphics)
    {
        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("Invocation observations require the current presentation process.");
        NativeBattleEffectMemory memory = process.Memory;
        NativeBattleActionPacketsState packets = process.ActionPackets;
        int? files = process.NativeFileIo?.ActiveCount;
        return new(graphics.Descriptors.Queue.Length, graphics.Descriptors.Tasks.Count(task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0), graphics.PendingContacts.Length, graphics.Effects.PublishedCount, graphics.Effects.Head is not null, graphics.Auxiliary.Head is not null, graphics.ModuleEffects.PublishedCount, graphics.ModuleEffects.Head is not null, files, memory.ReturnedTaskPointer1D96AAC, memory.DetachedReturnedTask is not null, process.NativeCameraTaskMask, process.ObserveNativeSoundBusy(), graphics.ActorModels.Entries.Select(entry => entry.State).Any(model => (model.Flags0 & 2) != 0 && (model.ResourceMagicId is not null || model.ActiveStagedResourceRow100 is not null)), ActiveAction is not null || actions.Count != 0, nativeTimedAction is not null || nativeActionCommitDepth != 0 || HasNativePendingReactions || pendingSlotsDeal is not null || nativeShotProcesses.Count != 0 || nativeDuelProcesses.Count != 0 || nativeRenzokukenProcesses.Count != 0)
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
