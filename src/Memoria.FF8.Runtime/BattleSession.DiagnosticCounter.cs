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
    private bool ObservingDiagnosticCounter => diagnosticCounterTracksContacts || diagnosticCounterCandidateObservations is not null || DiagnosticCounterContactPublished is not null || DiagnosticCounterCallbackObserved is not null || DiagnosticCounterCommandQueued is not null;
    private long diagnosticCounterPrimarySerial;
    private long diagnosticCounterQueueEpoch;
    private long diagnosticCounterAiSerial;
    private sealed record DiagnosticCounterContactFacts(string ActorId, byte ActorIndex, byte QueuedCommand, ushort ActionId, byte InternalCommand, int BeforeHp, int AfterHp, BattleDiagnosticCounterActor Recipient, bool Preliminary, byte Order);
    private bool diagnosticCounterTracksContacts;
    private readonly ConditionalWeakTable<NativeBattleContactPacket, DiagnosticCounterContactFacts> diagnosticCounterContacts = new();
    private readonly Dictionary<(string Actor, byte Order), long> diagnosticCounterPending = [];
    private readonly ConditionalWeakTable<BattleExpandedAiCallbackRequest, BattleDiagnosticCounterCallback> diagnosticCounterCallbacks = new();
    private List<BattleDiagnosticCounterCallbackObservation>? diagnosticCounterCandidateObservations;
    private BattleDiagnosticCounterCallback? diagnosticCounterCurrentCallback;
    private (long Serial, int Stage, string Sha)? diagnosticCounterAiScope;

    private static BattleDiagnosticCounterActor CaptureDiagnosticCounterActor(BattleCombatant actor) => new(actor.Id, checked((byte)actor.RequireNativeActorIndex()), actor.ModelId, actor.CurrentHp, actor.IsActive, actor.NativeCombatantFlags, actor.LastNativeAction, actor.NativePendingReaction, actor.AiVariables.ToImmutableArray()) { SavedStatuses = NativeSavedStatusBits(actor), BattleStatuses = NativeBattleStatusBits(actor) };

    private void ResetDiagnosticCounterOwners()
    {
        diagnosticCounterContacts.Clear();
        diagnosticCounterPending.Clear();
        diagnosticCounterCallbacks.Clear();
        diagnosticCounterCurrentCallback = null;
        diagnosticCounterAiScope = null;
    }

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
        if (command?.NativeAction is not { } native || contact.Actor.NativeActorIndex is not int actorIndex || recipient.NativeActorIndex is null)
        {
            return;
        }
        byte queued = command.NativeQueuedCommand ?? native.Command;
        contact.DiagnosticPrimary = new(contact.Actor.Id, checked((byte)actorIndex), queued, native.ActionId, native.Command, hp.Before, hp.After, CaptureDiagnosticCounterActor(recipient), contact.DiagnosticPreliminary, order);
    }

    private void ObserveDiagnosticCounterResolvedContact(NativeContactWorking working, NativeBattleContactPacket packet)
    {
        if (working.DiagnosticPrimary is { } primary)
        {
            diagnosticCounterContacts.Add(packet, primary);
        }
    }

    private void PublishDiagnosticCounterContacts(BattleSession candidate, BattlePresentationProcess process, ImmutableArray<BattleActionHeaderReference> admittedHeaders, BattleActionHeaderReference? triggerRecomputed = null)
    {
        if (!ObservingDiagnosticCounter)
        {
            return;
        }
        foreach (var contact in candidate.diagnosticCounterContacts)
        {
            diagnosticCounterContacts.AddOrUpdate(contact.Key, contact.Value);
        }
        NativeBattleActionPacketsState packets = process.ActionPackets;
        foreach (BattleActionHeaderReference reference in triggerRecomputed is { } triggerHeader ? admittedHeaders.Insert(0, triggerHeader) : admittedHeaders)
        {
            if (OriginalDiagnosticHeaderIndex(reference) is not int index || process.ReadDiagnosticCounterHeaderGeneration(index) is not long generation)
            {
                continue;
            }
            ExpandedHostedInvocation? invocation = FindDiagnosticCounterCast(reference);
            if (invocation is null)
            {
                continue;
            }
            NativeBattleActionHeaderSlot header = packets.ReadHeader(reference);
            for (int ordinal = 0; ordinal < invocation.Contacts.Length; ordinal++)
            {
                int occurrence = header.FirstContact.Ordinal + ordinal;
                if (occurrence >= NativeBattleContactAddress.NativeRowCount || !diagnosticCounterContacts.TryGetValue(invocation.Contacts[ordinal].Packet, out DiagnosticCounterContactFacts? facts))
                {
                    continue;
                }
                diagnosticCounterContacts.Remove(invocation.Contacts[ordinal].Packet);
                BattleDiagnosticCounterActor recipient = facts.Recipient;
                BattleDiagnosticCounterPrimary primary = new(++diagnosticCounterPrimarySerial, NativeHostCallbacks ?? Frame, facts.ActorId, facts.ActorIndex, recipient.Id, recipient.Index, facts.QueuedCommand, facts.ActionId, facts.InternalCommand, facts.BeforeHp, facts.AfterHp, index, generation, recipient, facts.Preliminary);
                if (!facts.Preliminary)
                {
                    diagnosticCounterPending[(recipient.Id, facts.Order)] = primary.Serial;
                }
                DiagnosticCounterContactPublished?.Invoke(new(primary, occurrence, triggerRecomputed == reference, packets.ExportRetained().DetachDiagnosticCopy()));
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
                FindDiagnosticCounterCast(reference) is not { Command.NativeAction: { } native } invocation ||
                NativeBattleTargetRequestAdapter.ProjectOriginalWord(invocation.Command.TargetRequest, ReadOriginalTargetSlot) is not ushort targets)
            {
                continue;
            }
            NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
            ushort selected = (ushort)(targets & 0x7F);
            observer(new(NativeHostCallbacks ?? Frame, scope, entered.Queue.Epoch, receipt.Request.Stage, sha, entered.Actor.Id, entered.Actor.Index, header.Command, header.Action, header.Effect, header.Sequence, targets, selected, native.AttackFlags, headerIndex, generation));
        }
    }

    private ExpandedHostedInvocation? FindDiagnosticCounterCast(BattleActionHeaderReference reference)
    {
        foreach (ExpandedHostedInvocation invocation in expandedOperationCursor.Operations.OfType<ExpandedHostedInvocation>())
        {
            if (ReadDiagnosticBoundCast(invocation, reference) is { } bound)
            {
                return bound;
            }
        }
        return null;
    }
}
