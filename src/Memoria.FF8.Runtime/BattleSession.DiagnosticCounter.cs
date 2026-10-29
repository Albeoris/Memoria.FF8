using System.Collections.Immutable;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattleDiagnosticCounterActor([property: JsonRequired] string Id, [property: JsonRequired] byte Index, [property: JsonRequired] int ModelId, [property: JsonRequired] int Hp, [property: JsonRequired] bool Active, [property: JsonRequired] uint CombatantFlags, [property: JsonRequired] NativeBattleLastAction? LastAction, [property: JsonRequired] NativeBattlePendingReaction PendingReaction, [property: JsonRequired] ImmutableArray<uint> Variables)
{
    [JsonRequired]
    public ushort SavedStatuses { get; init; }
    [JsonRequired]
    public uint BattleStatuses { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattleDiagnosticCounterPrimary([property: JsonRequired] long Serial, [property: JsonRequired] long Callback, [property: JsonRequired] string ActorId, [property: JsonRequired] byte ActorIndex, [property: JsonRequired] string RecipientId, [property: JsonRequired] byte RecipientIndex, [property: JsonRequired] byte QueuedCommand, [property: JsonRequired] ushort ActionId, [property: JsonRequired] byte InternalCommand, [property: JsonRequired] int BeforeHp, [property: JsonRequired] int AfterHp, [property: JsonRequired] int HeaderIndex, [property: JsonRequired] long HeaderGeneration, [property: JsonRequired] BattleDiagnosticCounterActor Recipient);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattleDiagnosticCounterContact([property: JsonRequired] BattleDiagnosticCounterPrimary Primary, [property: JsonRequired] int ContactIndex, [property: JsonRequired] bool TriggerRecomputed, [property: JsonRequired] NativeBattleActionPacketsState Packets);
public enum BattleDiagnosticCounterCallbackPhase
{
    Queued,
    Taken,
    Started,
    AiRejected,
    AiEntered,
    AiExited,
    Completed
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattleDiagnosticCounterCallback([property: JsonRequired] long Epoch, [property: JsonRequired] long Callback, [property: JsonRequired] NativeBattleCommandLocation Location, [property: JsonRequired] byte ActorIndex, [property: JsonRequired] ushort Stage, [property: JsonRequired] byte? PendingOrder, [property: JsonRequired] long? PrimarySerial, [property: JsonRequired] ImmutableArray<byte> Packet, [property: JsonRequired] ushort UnknownByteMask);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattleDiagnosticCounterCallbackObservation([property: JsonRequired] BattleDiagnosticCounterCallbackPhase Phase, [property: JsonRequired] long Callback, [property: JsonRequired] BattleDiagnosticCounterCallback Queue, [property: JsonRequired] BattleDiagnosticCounterActor Actor, [property: JsonRequired] int? AiStage = null, [property: JsonRequired] long? AiScope = null, [property: JsonRequired] string? ScriptSha256 = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattleDiagnosticCounterCommand([property: JsonRequired] long Callback, [property: JsonRequired] long AiScope, [property: JsonRequired] long CallbackEpoch, [property: JsonRequired] byte AiStage, [property: JsonRequired] string ScriptSha256, [property: JsonRequired] string ActorId, [property: JsonRequired] byte ActorIndex, [property: JsonRequired] byte Command, [property: JsonRequired] ushort ActionId, [property: JsonRequired] ushort EffectId, [property: JsonRequired] byte Sequence, [property: JsonRequired] ushort QueuedTargetMask, [property: JsonRequired] ushort SelectedTargetMask, [property: JsonRequired] byte AttackFlags, [property: JsonRequired] int HeaderIndex, [property: JsonRequired] long HeaderGeneration);

public sealed partial class BattleSession
{
    public event Action<BattleDiagnosticCounterContact>? DiagnosticCounterContactPublished;
    public event Action<BattleDiagnosticCounterCallbackObservation>? DiagnosticCounterCallbackObserved;
    public event Action<BattleDiagnosticCounterCommand>? DiagnosticCounterCommandQueued;
    private long diagnosticCounterPrimarySerial;
    private long diagnosticCounterQueueEpoch;
    private long diagnosticCounterAiSerial;
    private int? diagnosticCounterProducingHeader;
    private readonly ConditionalWeakTable<NativeBattleContactPacket, BattleDiagnosticCounterPrimary> diagnosticCounterContacts = new();
    private readonly Dictionary<(int Header, long Generation), List<(int Index, BattleDiagnosticCounterPrimary Primary)>> diagnosticCounterAppended = [];
    private readonly Dictionary<(byte Actor, byte Order), long> diagnosticCounterPending = [];
    private readonly Dictionary<NativeBattleCommandLocation, BattleDiagnosticCounterCallback> diagnosticCounterCallbacks = [];
    private BattleDiagnosticCounterCallback? diagnosticCounterCurrentCallback;
    private (long Serial, int Stage, string Sha)? diagnosticCounterAiScope;
    private bool ObservingDiagnosticCounter => DiagnosticCounterContactPublished is not null || DiagnosticCounterCallbackObserved is not null || DiagnosticCounterCommandQueued is not null;

    private static BattleDiagnosticCounterActor CaptureDiagnosticCounterActor(BattleCombatant actor) => new(actor.Id, checked((byte)actor.NativeIndex), actor.ModelId, actor.CurrentHp, actor.IsActive, actor.NativeCombatantFlags, actor.LastNativeAction, actor.NativePendingReaction, actor.AiVariables.ToImmutableArray()) { SavedStatuses = NativeSavedStatusBits(actor), BattleStatuses = NativeBattleStatusBits(actor) };

    private void ResetDiagnosticCounterOwners()
    {
        diagnosticCounterContacts.Clear();
        diagnosticCounterAppended.Clear();
        diagnosticCounterPending.Clear();
        diagnosticCounterCallbacks.Clear();
        diagnosticCounterCurrentCallback = null;
        diagnosticCounterAiScope = null;
        diagnosticCounterProducingHeader = null;
    }

    private long? DiagnosticCounterHeaderGeneration(int header) => diagnosticHeaderGenerations is { } generations && generations.TryGetValue(header, out long generation) ? generation : null;

    private void ObserveDiagnosticCounterResolution(BattleCombatant? actor, BattleCombatant target, int before, int after)
    {
        if (ObservingDiagnosticCounter && nativeContactWorking is { CommittingSecondary: false } contact && contact.Actor == actor && contact.Recipient == target)
        {
            contact.DiagnosticHp = (before, after);
        }
    }

    private void ObserveDiagnosticCounterReaction(BattleCombatant recipient, int stage)
    {
        if (!ObservingDiagnosticCounter || stage != 2 || nativeContactWorking is not { CommittingSecondary: false, PrimaryCommitted: true, DiagnosticHp: { } hp } contact || contact.Recipient != recipient || recipient.NativePendingReaction is not { Stage: 2, Order: byte order })
        {
            return;
        }
        BattleActionPresentation? command = resolvingNativeAction ?? ActiveAction;
        int? header = diagnosticCounterProducingHeader ?? command?.NativeHeaderIndex;
        if (command?.NativeAction is not { } native || header is not int index || DiagnosticCounterHeaderGeneration(index) is not long generation)
        {
            return;
        }
        byte queued = command.NativeQueuedCommand ?? native.Command;
        BattleDiagnosticCounterPrimary primary = new(++diagnosticCounterPrimarySerial, NativeHostCallbacks ?? Frame, contact.Actor.Id, checked((byte)contact.Actor.NativeIndex), recipient.Id, checked((byte)recipient.NativeIndex), queued, native.ActionId, native.Command, hp.Before, hp.After, index, generation, CaptureDiagnosticCounterActor(recipient));
        contact.DiagnosticPrimary = primary;
        diagnosticCounterPending[(primary.RecipientIndex, order)] = primary.Serial;
    }

    private void ObserveDiagnosticCounterResolvedContact(NativeContactWorking working, NativeBattleContactPacket packet)
    {
        if (working.DiagnosticPrimary is { } primary)
        {
            diagnosticCounterContacts.Add(packet, primary);
        }
    }

    private void ObserveDiagnosticCounterAppend(NativeBattleContactPacket packet, int index, bool triggerRecomputed = false)
    {
        if (!diagnosticCounterContacts.TryGetValue(packet, out BattleDiagnosticCounterPrimary? primary))
        {
            return;
        }
        diagnosticCounterContacts.Remove(packet);
        if (DiagnosticCounterHeaderGeneration(primary.HeaderIndex) != primary.HeaderGeneration)
        {
            throw new InvalidDataException("Counter contact publication lost its actual prepared header generation.");
        }
        if (triggerRecomputed)
        {
            DiagnosticCounterContactPublished?.Invoke(new(primary, index, true, nativeActionPackets.DetachDiagnosticCopy()));
            return;
        }
        var key = (primary.HeaderIndex, primary.HeaderGeneration);
        if (!diagnosticCounterAppended.TryGetValue(key, out var appended))
        {
            appended = [];
            diagnosticCounterAppended.Add(key, appended);
        }
        appended.Add((index, primary));
    }

    private void ObserveDiagnosticCounterPublishedHeaders(int first)
    {
        if (!ObservingDiagnosticCounter)
        {
            return;
        }
        for (int header = first; header < nativeActionPackets.ActionCount; header++)
        {
            if (DiagnosticCounterHeaderGeneration(header) is not long generation || !diagnosticCounterAppended.Remove((header, generation), out var appended))
            {
                continue;
            }
            foreach (var contact in appended)
            {
                DiagnosticCounterContactPublished?.Invoke(new(contact.Primary, contact.Index, false, nativeActionPackets.DetachDiagnosticCopy()));
            }
        }
    }

    private void ObserveDiagnosticCounterCallbackQueued(BattleCombatant actor, ushort stage, NativeBattlePendingReaction pending, NativeBattleCommandLocation location)
    {
        if (!ObservingDiagnosticCounter)
        {
            return;
        }
        NativeBattleCommandPacket packet = nativeCommandBanks.Banks[location.Bank].Packets[2 * location.Slot];
        long? primary = stage == 2 && pending is { Stage: 2, Order: byte order } && diagnosticCounterPending.Remove((checked((byte)actor.NativeIndex), order), out long serial) ? serial : null;
        BattleDiagnosticCounterCallback queue = new(++diagnosticCounterQueueEpoch, NativeHostCallbacks ?? Frame, location with { }, checked((byte)actor.NativeIndex), stage, pending.Order, primary, ImmutableArray.CreateRange(packet.Bytes.AsSpan().ToArray()), packet.UnknownByteMask);
        diagnosticCounterCallbacks[location] = queue;
        DiagnosticCounterCallbackObserved?.Invoke(new(BattleDiagnosticCounterCallbackPhase.Queued, NativeHostCallbacks ?? Frame, queue, CaptureDiagnosticCounterActor(actor)));
    }

    private void ObserveDiagnosticCounterCallbackTaken(NativeBattleCommandLocation location, NativeBattleCommandPacket packet)
    {
        diagnosticCounterCurrentCallback = null;
        if (!ObservingDiagnosticCounter || packet.ReadByte(1) != 255 || !diagnosticCounterCallbacks.Remove(location, out var queue))
        {
            return;
        }
        if (packet.ReadByte(0) != queue.ActorIndex || packet.ReadWord(4) != queue.Stage || packet.UnknownByteMask != queue.UnknownByteMask || !packet.Bytes.AsSpan().SequenceEqual(queue.Packet.AsSpan()))
        {
            throw new InvalidDataException("Counter callback physical queue epoch no longer owns its actual bank/slot packet.");
        }
        diagnosticCounterCurrentCallback = queue;
        BattleCombatant actor = FindNativeActor(queue.ActorIndex);
        DiagnosticCounterCallbackObserved?.Invoke(new(BattleDiagnosticCounterCallbackPhase.Taken, NativeHostCallbacks ?? Frame, queue, CaptureDiagnosticCounterActor(actor)));
    }

    private void ObserveDiagnosticCounterCallback(BattleCombatant actor, ushort stage, BattleDiagnosticCounterCallbackPhase phase)
    {
        if (diagnosticCounterCurrentCallback is { } queue && queue.ActorIndex == actor.NativeIndex && queue.Stage == stage)
        {
            DiagnosticCounterCallbackObserved?.Invoke(new(phase, NativeHostCallbacks ?? Frame, queue, CaptureDiagnosticCounterActor(actor)));
        }
    }

    private void ObserveDiagnosticCounterAi(BattleCombatant actor, int stage, BattleDiagnosticCounterCallbackPhase phase, long? scope = null, string? sha = null)
    {
        if (diagnosticCounterCurrentCallback is { } queue && ObservingDiagnosticCounter)
        {
            DiagnosticCounterCallbackObserved?.Invoke(new(phase, NativeHostCallbacks ?? Frame, queue, CaptureDiagnosticCounterActor(actor), stage, scope, sha));
        }
    }

    private void ObserveDiagnosticCounterCommand(BattleCombatant actor, BattleAttackCommand command, ushort? selectedTargetMask)
    {
        if (DiagnosticCounterCommandQueued is not { } observer || diagnosticCounterAiScope is not { } scope || diagnosticCounterCurrentCallback is not { } queue || command.NativeAiCallbackStage != scope.Stage || command.NativeHeaderIndex is not int header || DiagnosticCounterHeaderGeneration(header) is not long generation || command.NativeAction is not { } native || command.NativeQueuedCommand is not byte queued || command.NativeSequence is not byte sequence || selectedTargetMask is not ushort selected)
        {
            return;
        }
        observer(new(NativeHostCallbacks ?? Frame, scope.Serial, queue.Epoch, checked((byte)scope.Stage), scope.Sha, actor.Id, checked((byte)actor.NativeIndex), queued, native.ActionId, native.MagicId, sequence, command.NativeTargetMask, selected, native.AttackFlags, header, generation));
    }
}
