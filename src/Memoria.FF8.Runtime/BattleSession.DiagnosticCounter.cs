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
public sealed record BattleDiagnosticCounterPrimary([property: JsonRequired] long Serial, [property: JsonRequired] long Callback, [property: JsonRequired] string ActorId, [property: JsonRequired] byte ActorIndex, [property: JsonRequired] string RecipientId, [property: JsonRequired] byte RecipientIndex, [property: JsonRequired] byte QueuedCommand, [property: JsonRequired] ushort ActionId, [property: JsonRequired] byte InternalCommand, [property: JsonRequired] int BeforeHp, [property: JsonRequired] int AfterHp, [property: JsonRequired] int HeaderIndex, [property: JsonRequired] long HeaderGeneration, [property: JsonRequired] BattleDiagnosticCounterActor Recipient, [property: JsonRequired] bool Preliminary);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattleDiagnosticCounterContact([property: JsonRequired] BattleDiagnosticCounterPrimary Primary, [property: JsonRequired] int ContactIndex, [property: JsonRequired] bool TriggerRecomputed, [property: JsonRequired] NativeBattleRetainedActionPackets Packets);
public enum BattleDiagnosticCounterCallbackPhase
{
    Queued,
    Started,
    AiRejected,
    AiEntered,
    AiExited,
    Completed
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattleDiagnosticCounterCallback([property: JsonRequired] long Epoch, [property: JsonRequired] long Callback, [property: JsonRequired] string ActorId, [property: JsonRequired] BattleRosterSlot Slot, [property: JsonRequired] byte ActorIndex, [property: JsonRequired] ushort Stage, [property: JsonRequired] byte? PendingOrder, [property: JsonRequired] long? PrimarySerial);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattleDiagnosticCounterCallbackObservation([property: JsonRequired] BattleDiagnosticCounterCallbackPhase Phase, [property: JsonRequired] long Callback, [property: JsonRequired] BattleDiagnosticCounterCallback Queue, [property: JsonRequired] BattleDiagnosticCounterActor Actor, [property: JsonRequired] int? AiStage = null, [property: JsonRequired] long? AiScope = null, [property: JsonRequired] string? ScriptSha256 = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattleDiagnosticCounterCommand([property: JsonRequired] long Callback, [property: JsonRequired] long AiScope, [property: JsonRequired] long CallbackEpoch, [property: JsonRequired] byte AiStage, [property: JsonRequired] string ScriptSha256, [property: JsonRequired] string ActorId, [property: JsonRequired] byte ActorIndex, [property: JsonRequired] byte Command, [property: JsonRequired] ushort ActionId, [property: JsonRequired] ushort EffectId, [property: JsonRequired] byte Sequence, [property: JsonRequired] ushort QueuedTargetMask, [property: JsonRequired] ushort SelectedTargetMask, [property: JsonRequired] byte AttackFlags, [property: JsonRequired] int HeaderIndex, [property: JsonRequired] long HeaderGeneration);

public sealed partial class BattleSession
{
    public event Action<BattleDiagnosticCounterContact>? DiagnosticCounterContactPublished;
    public event Action<BattleDiagnosticCounterCallbackObservation>? DiagnosticCounterCallbackObserved;
    public event Action<BattleDiagnosticCounterCommand>? DiagnosticCounterCommandQueued;
    private bool ObservingDiagnosticCounter => diagnosticCounterCandidateObservations is not null || DiagnosticCounterContactPublished is not null || DiagnosticCounterCallbackObserved is not null || DiagnosticCounterCommandQueued is not null;
    private long diagnosticCounterPrimarySerial;
    private long diagnosticCounterQueueEpoch;
    private long diagnosticCounterAiSerial;
    private BattleActionHeaderReference? diagnosticCounterProducingHeader;
    private readonly ConditionalWeakTable<NativeBattleContactPacket, BattleDiagnosticCounterPrimary> diagnosticCounterContacts = new();
    private readonly Dictionary<(int Header, long Generation), List<(int Index, BattleDiagnosticCounterPrimary Primary)>> diagnosticCounterAppended = [];
    private readonly Dictionary<(string Actor, byte Order), long> diagnosticCounterPending = [];
    private readonly ConditionalWeakTable<BattleExpandedAiCallbackRequest, BattleDiagnosticCounterCallback> diagnosticCounterCallbacks = new();
    private List<BattleDiagnosticCounterCallbackObservation>? diagnosticCounterCandidateObservations;
    private BattleDiagnosticCounterCallback? diagnosticCounterCurrentCallback;
    private (long Serial, int Stage, string Sha)? diagnosticCounterAiScope;

    private static BattleDiagnosticCounterActor CaptureDiagnosticCounterActor(BattleCombatant actor) => new(actor.Id, checked((byte)actor.RequireNativeActorIndex()), actor.ModelId, actor.CurrentHp, actor.IsActive, actor.NativeCombatantFlags, actor.LastNativeAction, actor.NativePendingReaction, actor.AiVariables.ToImmutableArray()) { SavedStatuses = NativeSavedStatusBits(actor), BattleStatuses = NativeBattleStatusBits(actor) };

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
        int? header = OriginalDiagnosticHeaderIndex(diagnosticCounterProducingHeader ?? command?.NativeHeader);
        if (command?.NativeAction is not { } native || header is not int index || DiagnosticCounterHeaderGeneration(index) is not long generation)
        {
            return;
        }
        byte queued = command.NativeQueuedCommand ?? native.Command;
        BattleDiagnosticCounterPrimary primary = new(++diagnosticCounterPrimarySerial, NativeHostCallbacks ?? Frame, contact.Actor.Id, checked((byte)contact.Actor.RequireNativeActorIndex()), recipient.Id, checked((byte)recipient.RequireNativeActorIndex()), queued, native.ActionId, native.Command, hp.Before, hp.After, index, generation, CaptureDiagnosticCounterActor(recipient), contact.DiagnosticPreliminary);
        contact.DiagnosticPrimary = primary;
        diagnosticCounterPending[(primary.RecipientId, order)] = primary.Serial;
    }

    private void ObserveDiagnosticCounterResolvedContact(NativeContactWorking working, NativeBattleContactPacket packet)
    {
        if (working.DiagnosticPrimary is { } primary)
        {
            diagnosticCounterContacts.Add(packet, primary);
        }
    }

    private void ObserveDiagnosticCounterAppend(NativeBattleContactPacket packet, BattleContactOccurrence occurrence, bool triggerRecomputed = false)
    {
        if (!diagnosticCounterContacts.TryGetValue(packet, out BattleDiagnosticCounterPrimary? primary))
        {
            return;
        }
        diagnosticCounterContacts.Remove(packet);
        if (occurrence.Ordinal >= NativeBattleContactAddress.NativeRowCount)
        {
            return;
        }
        int index = occurrence.Ordinal;
        if (DiagnosticCounterHeaderGeneration(primary.HeaderIndex) != primary.HeaderGeneration)
        {
            throw new InvalidDataException("Counter contact publication lost its actual prepared header generation.");
        }
        if (triggerRecomputed)
        {
            DiagnosticCounterContactPublished?.Invoke(new(primary, index, true, nativeActionPackets.ExportRetained().DetachDiagnosticCopy()));
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

    private void ObserveDiagnosticCounterPublishedHeaders(BattleActionHeaderReference first)
    {
        if (!ObservingDiagnosticCounter || OriginalDiagnosticHeaderIndex(first) is not int original)
        {
            return;
        }
        for (int header = original; header < Math.Min(nativeActionPackets.ActionCount, NativeBattleRetainedActionPackets.HeaderCapacity); header++)
        {
            if (DiagnosticCounterHeaderGeneration(header) is not long generation || !diagnosticCounterAppended.Remove((header, generation), out var appended))
            {
                continue;
            }
            foreach (var contact in appended)
            {
                DiagnosticCounterContactPublished?.Invoke(new(contact.Primary, contact.Index, false, nativeActionPackets.ExportRetained().DetachDiagnosticCopy()));
            }
        }
    }

    private void ObserveDiagnosticParticipantCallback(BattleParticipantCallbackReceipt receipt)
    {
        if (!ObservingDiagnosticCounter || FindTargetActor(receipt.Request.Actor) is not { IsPlayer: false, Slot: { } slot, NativeActorIndex: int alias } actor)
        {
            return;
        }
        long callback = NativeHostCallbacks ?? Frame;
        if (receipt.Phase == BattleParticipantCallbackPhase.Queued)
        {
            long? primary = receipt.Request.Stage == 2 && receipt.Reaction is { Stage: 2, Order: byte order } &&
                diagnosticCounterPending.Remove((actor.Id, order), out long serial) ? serial : null;
            BattleDiagnosticCounterCallback queue = new(++diagnosticCounterQueueEpoch, callback, actor.Id, slot, checked((byte)alias), receipt.Request.Stage, receipt.Reaction?.Order, primary);
            diagnosticCounterCallbacks.Add(receipt.Request, queue);
            DiagnosticCounterCallbackObserved?.Invoke(new(BattleDiagnosticCounterCallbackPhase.Queued, callback, queue, CaptureDiagnosticCounterActor(actor)));
        }
        else if (diagnosticCounterCallbacks.TryGetValue(receipt.Request, out BattleDiagnosticCounterCallback? queue))
        {
            BattleDiagnosticCounterCallbackPhase phase = receipt.Phase == BattleParticipantCallbackPhase.Started
                ? BattleDiagnosticCounterCallbackPhase.Started
                : BattleDiagnosticCounterCallbackPhase.Completed;
            DiagnosticCounterCallbackObserved?.Invoke(new(phase, callback, queue, CaptureDiagnosticCounterActor(actor)));
            if (phase == BattleDiagnosticCounterCallbackPhase.Completed)
            {
                diagnosticCounterCallbacks.Remove(receipt.Request);
            }
        }
    }

    private void PrepareDiagnosticCounterCandidate(BattleSession candidate, BattleExpandedAiCallbackRequest request)
    {
        if (diagnosticCounterCallbacks.TryGetValue(request, out BattleDiagnosticCounterCallback? queue))
        {
            candidate.diagnosticCounterCurrentCallback = queue;
            candidate.diagnosticCounterCandidateObservations = [];
            candidate.diagnosticCounterAiSerial = diagnosticCounterAiSerial;
        }
    }

    private void ObserveDiagnosticCounterAi(BattleCombatant actor, int stage, BattleDiagnosticCounterCallbackPhase phase, long? scope = null, string? sha = null)
    {
        if (diagnosticCounterCurrentCallback is { } queue && diagnosticCounterCandidateObservations is { } observations)
        {
            observations.Add(new(phase, NativeHostCallbacks ?? Frame, queue, CaptureDiagnosticCounterActor(actor), stage, scope, sha));
        }
    }

    private void PublishDiagnosticCounterCandidate(BattleSession candidate, BattleParticipantCallbackReceipt receipt)
    {
        if (candidate.diagnosticCounterCandidateObservations is not { } observations)
        {
            return;
        }
        diagnosticCounterAiSerial = candidate.diagnosticCounterAiSerial;
        foreach (BattleDiagnosticCounterCallbackObservation observation in observations)
        {
            DiagnosticCounterCallbackObserved?.Invoke(observation);
        }
        if (DiagnosticCounterCommandQueued is not { } observer || observations.FirstOrDefault(observation => observation.Phase == BattleDiagnosticCounterCallbackPhase.AiEntered) is not { AiScope: long scope, ScriptSha256: { } sha } entered ||
            Presentation is not { } process)
        {
            return;
        }
        foreach (BattleActionHeaderReference reference in receipt.Headers)
        {
            if (OriginalDiagnosticHeaderIndex(reference) is not int headerIndex || process.ReadDiagnosticCounterHeaderGeneration(headerIndex) is not long generation ||
                !process.ActionPackets.HasPublishedHeader(reference) ||
                expandedOperationCursor.Operations.OfType<ExpandedHostedInvocation>().SingleOrDefault(invocation => invocation.HeaderReference == reference) is not { Command.NativeAction: { } native } invocation ||
                NativeBattleTargetRequestAdapter.ProjectOriginalWord(invocation.Command.TargetRequest, ReadOriginalTargetSlot) is not ushort targets)
            {
                continue;
            }
            NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
            ushort selected = (ushort)(targets & 0x7F);
            observer(new(NativeHostCallbacks ?? Frame, scope, entered.Queue.Epoch, receipt.Request.Stage, sha, entered.Actor.Id, entered.Actor.Index, header.Command, header.Action, header.Effect, header.Sequence, targets, selected, native.AttackFlags, headerIndex, generation));
        }
    }
}
