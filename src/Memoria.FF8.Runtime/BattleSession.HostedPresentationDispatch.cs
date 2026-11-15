using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal sealed record HostedDispatchHeader(NativeBattleActionHeaderPreparation Header, ImmutableArray<NativeBattleContactPacket> Contacts);
    internal sealed record HostedDispatchCompletion(uint Callback, BattleModelOwner Owner, string ActorId, bool CapturedBeforeAi = false);
    internal abstract record HostedDispatchControl
    {
        internal sealed record Barrier : HostedDispatchControl;
        internal sealed record ResetCounts : HostedDispatchControl;
        internal sealed record Model(ushort Command, BattleModelOwner Owner) : HostedDispatchControl;
        internal sealed record LoadEnemy(BattleEnemyActorModel Owner, NativeBattleModelSlot Slot, int SceneSlot, int ModelId) : HostedDispatchControl;
        internal sealed record AllocationStatistics : HostedDispatchControl;
        internal sealed record ActivateOriginal(NativeBattleModelSlot Slot) : HostedDispatchControl;
        internal sealed record AllocationGate : HostedDispatchControl;
        internal sealed record Message(BattleEvent Event, byte Tag, byte Priority = 128) : HostedDispatchControl;
        internal sealed record ShotReadiness(uint Callback) : HostedDispatchControl;
        internal sealed record Complete(HostedDispatchCompletion Completion) : HostedDispatchControl;
    }

    internal sealed record HostedDispatchControls(ImmutableArray<HostedDispatchControl> BeforeAction, ImmutableArray<HostedDispatchControl> AfterAction);
    internal sealed record HostedControlPacket(int Slot, byte Serial, ushort Command, uint? Callback, bool Retired = false);
    private ImmutableArray<HostedControlPacket> hostedControlPackets = [];
    internal sealed record HostedDispatchAction(HostedPresentationInvocation Invocation, BattleModelOwner SourceOwner, NativeBattleGraphicsResource Source, byte? ModelId, bool IsParty, int PartyActorCount);

    internal sealed class PreparedHostedDispatch(BattleSession owner, NativeBattleGraphicsState graphics, NativeBattleActionPacketsState originalPackets, long epoch, long nextEpoch, long issuedAction, long issuedCompletion, NativeBattleGraphicsState admitted, NativeBattleActionPacketsState packets, HostedDispatchAction? action, HostedCompletionInvocation? completion, PreparedHostedCompletionAck? acknowledgment, ImmutableArray<BattleActionHeaderReference> appendedHeaders, ImmutableArray<HostedControlPacket> controls, PreparedHostedModelRegistry? registry)
    {
        internal BattleSession Owner { get; } = owner;
        internal NativeBattleGraphicsState Graphics { get; } = graphics;
        internal NativeBattleActionPacketsState OriginalPackets { get; } = originalPackets;
        internal long Epoch { get; } = epoch;
        internal long NextEpoch { get; } = nextEpoch;
        internal long IssuedAction { get; } = issuedAction;
        internal long IssuedCompletion { get; } = issuedCompletion;
        internal NativeBattleGraphicsState Admitted { get; } = admitted;
        internal NativeBattleActionPacketsState Packets { get; } = packets;
        internal HostedDispatchAction? Action { get; } = action;
        internal HostedCompletionInvocation? Completion { get; } = completion;
        internal PreparedHostedCompletionAck? Acknowledgment { get; } = acknowledgment;
        internal ImmutableArray<BattleActionHeaderReference> AppendedHeaders { get; } = appendedHeaders;
        internal ImmutableArray<HostedControlPacket> Controls { get; } = controls;
        internal PreparedHostedModelRegistry? Registry { get; } = registry;
    }

    internal PreparedHostedDispatch PrepareHostedDispatch(ImmutableArray<HostedDispatchHeader> append, BattleActionHeaderReference? headerReference = null, bool dispatchAppended = false, HostedDispatchCompletion? completion = null, PreparedHostedCompletionAck? acknowledgment = null, bool freshActionPackets = false, HostedDispatchControls? controls = null, bool publishEventHeaders = false, ImmutableArray<HostedModelLoadAdmission> allocations = default)
    {
        if (!hostedBattlePresentation || NativeGraphics is not { EntryReady: true } graphics ||
            hostedFrameServices is not null || hostedPresentationInvocation is not null || hostedCompletionInvocation is not null && acknowledgment is null ||
            !HostedControlPrefixRetired && (controls is not null || acknowledgment is null || headerReference is not null || dispatchAppended || completion is not null))
        {
            throw new InvalidOperationException("Hosted dispatch requires an idle presentation process after entry.");
        }
        if (append.IsDefault || freshActionPackets && acknowledgment is not null || dispatchAppended && (append.IsEmpty || headerReference is not null) ||
            completion is not null && controls is not null ||
            headerReference is null && !dispatchAppended && completion is null && acknowledgment is null && controls is null)
        {
            throw new InvalidDataException("Hosted dispatch requires explicit headers and a dispatch or acknowledgement.");
        }
        PreparedHostedModelRegistry? allocationRegistry = PrepareHostedModelRegistry(graphics, allocations);
        HostedModelRegistry? registry = allocationRegistry?.Replacement ?? hostedModelRegistry;
        BattleModelRoster roster = registry?.Roster ?? graphics.ActorModels.Roster;
        if (acknowledgment is not null)
        {
            ValidatePreparedHostedCompletionAck(acknowledgment);
        }

        NativeBattleActionPacketsState original = nativeActionPackets;
        NativeBattleActionPacketsState packets = acknowledgment?.AcknowledgedPackets ?? original;
        if (!packets.Roster.Owners.SequenceEqual((allocationRegistry?.Original.Roster ?? roster).Owners))
        {
            throw new InvalidDataException("Hosted packet storage differs from its graphics roster.");
        }
        NativeBattleModelAliases aliases = graphics.NativeBindings.CaptureAliases(graphics.ActorModels, roster);
        if (freshActionPackets)
        {
            packets = NativeBattleActionPacketsState.Create(roster, packets.ExportRetained(), aliases);
        }
        else if (!append.IsEmpty || allocationRegistry is not null)
        {
            packets = packets.WithRoster(roster, aliases);
        }
        (packets, ImmutableArray<BattleActionHeaderReference> appended) = AppendHostedHeaders(graphics, packets, append, publishEventHeaders, registry);
        if (dispatchAppended)
        {
            headerReference = appended[0];
        }

        NativeBattleDescriptorState descriptors = graphics.Descriptors;
        HostedCompletionInvocation? nextCompletion = null;
        ImmutableArray<HostedControlPacket>.Builder queuedControls = ImmutableArray.CreateBuilder<HostedControlPacket>();
        if (controls is not null)
        {
            EnqueueControls(controls.BeforeAction);
        }
        HostedDispatchAction? action = null;
        if (headerReference is BattleActionHeaderReference selected)
        {
            (BattleModelOwner owner, NativeBattleGraphicsResource source) = ReadHostedDispatchSource(graphics, packets, selected, registry);
            (descriptors, int slot) = descriptors.EnqueueAction(128, selected);
            HostedPresentationInvocation invocation = new(checked(lastHostedActionIssued + 1), slot, descriptors.Packets[slot].Serial, selected);
            byte? modelId = graphics.ActorModels.TryGet(owner, out NativeBattleGraphicsModel? model) ? model.ModelId4 : FindHostedAllocationBinding(registry, owner)?.ModelId;
            action = new(invocation, owner, source, modelId, owner.Side == BattleSide.Allies, roster.Party.Count());
        }
        if (controls is not null)
        {
            EnqueueControls(controls.AfterAction);
        }
        else if (completion is not null)
        {
            EnqueueCompletion(completion);
        }
        NativeBattleGraphicsState admitted = ReferenceEquals(graphics.Descriptors, descriptors) ? graphics : graphics with { Descriptors = descriptors };
        ImmutableArray<HostedControlPacket> prefix = HostedControlPrefixRetired ? queuedControls.ToImmutable() : hostedControlPackets;
        return new(this, graphics, original, hostedPreparationEpoch, checked(hostedPreparationEpoch + 1), lastHostedActionIssued, lastHostedCompletionIssued, admitted, packets, action, nextCompletion, acknowledgment, appended, prefix, allocationRegistry);

        void EnqueueControls(ImmutableArray<HostedDispatchControl> operations)
        {
            foreach (HostedDispatchControl operation in operations)
            {
                int slot;
                switch (operation)
                {
                    case HostedDispatchControl.Complete finish:
                        EnqueueCompletion(finish.Completion);
                        continue;
                    case HostedDispatchControl.Barrier:
                        (descriptors, slot) = descriptors.Enqueue(112, 128, 0);
                        break;
                    case HostedDispatchControl.ResetCounts:
                        (descriptors, slot) = descriptors.Enqueue(10, 128, 0x47DD90);
                        break;
                    case HostedDispatchControl.Model model when model.Command is 105 or 106 or 113 && roster.Contains(model.Owner):
                        uint argument = model.Command == 113 ? 0x487670u : 0;
                        (descriptors, slot) = descriptors.EnqueueModel(model.Command, 128, argument, model.Owner, aliases);
                        break;
                    case HostedDispatchControl.LoadEnemy load when registry?.Loads.TryGetValue(load.Owner, out HostedModelLoadAdmission? admission) == true && admission.Load == load:
                        NativeBattleDescriptorModelSubject.PlannedLoad plan = new(load.Owner, load.Slot, admission.Binding);
                        (descriptors, slot) = descriptors.EnqueuePlannedLoad(102, 128, NativeParticipantAddress(load.Slot.Offset), plan, admission.Binding.ModelId);
                        break;
                    case HostedDispatchControl.AllocationStatistics:
                        (descriptors, slot) = descriptors.Enqueue(113, 128, 0x48AD10);
                        break;
                    case HostedDispatchControl.ActivateOriginal activate:
                        (descriptors, slot) = descriptors.Enqueue(113, 128, 0x487670, checked((ushort)activate.Slot.Offset));
                        break;
                    case HostedDispatchControl.AllocationGate:
                        (descriptors, slot) = descriptors.Enqueue(10, 128, 0x4876B0);
                        break;
                    case HostedDispatchControl.Message message:
                        (descriptors, slot) = descriptors.EnqueueMessage(message.Event, message.Priority, message.Tag);
                        break;
                    case HostedDispatchControl.ShotReadiness { Callback: 0x48D1A0 or 0x4AD7C0 } shot:
                        (descriptors, slot) = descriptors.Enqueue(10, 128, shot.Callback);
                        break;
                    default:
                        throw new InvalidDataException("Hosted dispatch control lacks its original command or loaded model owner.");
                }
                NativeBattleDescriptorPacket packet = descriptors.Packets[slot];
                queuedControls.Add(new(slot, packet.Serial, packet.Command, packet.Command == 10 ? packet.Argument : null));
            }
        }

        void EnqueueCompletion(HostedDispatchCompletion finish)
        {
            if (nextCompletion is not null || !IsHostedCompletionCallback(finish.Callback))
            {
                throw new ArgumentOutOfRangeException(nameof(completion), "Hosted dispatch requires at most one original combat callback.");
            }
            if ((!finish.CapturedBeforeAi && !roster.Contains(finish.Owner)) || !StringComparer.Ordinal.Equals(RequireCanonicalGraphicsActorId(finish.Owner), finish.ActorId))
            {
                throw new InvalidDataException("Hosted completion requires its canonical bound actor.");
            }
            (descriptors, int slot) = descriptors.Enqueue(10, 128, finish.Callback);
            nextCompletion = new(new(checked(lastHostedCompletionIssued + 1), finish.Callback, finish.Owner, finish.ActorId), slot, descriptors.Packets[slot].Serial, CapturedBeforeAi: finish.CapturedBeforeAi);
        }
    }

    internal bool HostedControlPrefixRetired => hostedControlPackets.All(packet => packet.Retired);

    internal void AppendHostedScriptMessage(BattleEvent message, byte tag, byte priority)
    {
        if (!hostedBattlePresentation || hostedFrameServices is not null || NativeGraphics is not { EntryReady: true } graphics)
        {
            throw new InvalidOperationException("Hosted script text requires the current entered graphics process at canonical handoff.");
        }
        RequireHostedCompletionAcknowledged();
        NativeBattleGraphicsState prepared = QueueNativeDescriptorMessage(graphics, message, tag, priority);
        long nextEpoch = checked(hostedPreparationEpoch + 1);
        NativeGraphics = prepared;
        hostedPreparationEpoch = nextEpoch;
    }

    private void ObserveHostedControlPacketRemoved(int slot, NativeBattleDescriptorPacket packet)
    {
        for (int i = 0; i < hostedControlPackets.Length; i++)
        {
            HostedControlPacket control = hostedControlPackets[i];
            if (!control.Retired && control.Slot == slot && control.Serial == packet.Serial && control.Command == packet.Command &&
                (control.Callback is not uint callback || callback == packet.Argument))
            {
                hostedControlPackets = hostedControlPackets.SetItem(i, control with { Retired = true });
                return;
            }
        }
    }

    private (NativeBattleActionPacketsState Packets, ImmutableArray<BattleActionHeaderReference> Appended) AppendHostedHeaders(NativeBattleGraphicsState graphics, NativeBattleActionPacketsState packets, ImmutableArray<HostedDispatchHeader> append, bool publishEventHeaders, HostedModelRegistry? registry = null)
    {
        ImmutableArray<BattleActionHeaderReference>.Builder appended = ImmutableArray.CreateBuilder<BattleActionHeaderReference>(append.Length);
        foreach (HostedDispatchHeader write in append)
        {
            if (write.Contacts.IsDefault)
            {
                throw new InvalidDataException("Hosted action admission requires its explicit contact list.");
            }
            (packets, BattleActionHeaderReference reference) = packets.Prepare(write.Header);
            packets = packets.AppendContacts(write.Contacts);
            (packets, _) = packets.Publish(write.Contacts.Length);
            packets = packets.WritePlayerRepeatMode(reference, 1);
            if (!publishEventHeaders)
            {
                _ = ReadHostedDispatchSource(graphics, packets, reference, registry);
            }
            else
            {
                (FindHostedAllocationBinding(registry, write.Header.Source)?.Resource ?? ReadHostedPresentationModelResource(write.Header.Source) ?? throw new InvalidDataException("Hosted action header requires its original source resource.")).Validate();
            }
            appended.Add(reference);
        }
        return (packets, appended.MoveToImmutable());
    }

    private (BattleModelOwner Owner, NativeBattleGraphicsResource Source) ReadHostedDispatchSource(NativeBattleGraphicsState graphics, NativeBattleActionPacketsState packets, BattleActionHeaderReference reference, HostedModelRegistry? registry)
    {
        NativeBattleActionGraphicsState context = NativeBattleActionGraphics.Normalize(packets, reference, graphics.ActionGraphics);
        NativeBattleGraphicsResource source = graphics.ActorModels.Contains(context.Actor)
            ? NativeActionResource(graphics, context.Actor)
            : FindHostedAllocationBinding(registry, context.Actor)?.Resource ?? throw new InvalidDataException("Hosted dispatch lost its admitted source model.");
        source.Validate();
        return (context.Actor, source);
    }

    internal void ValidatePreparedHostedDispatch(PreparedHostedDispatch prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (!ReferenceEquals(prepared.Owner, this) || !ReferenceEquals(prepared.Graphics, NativeGraphics) ||
            !ReferenceEquals(prepared.OriginalPackets, nativeActionPackets) || prepared.Epoch != hostedPreparationEpoch ||
            prepared.IssuedAction != lastHostedActionIssued || prepared.IssuedCompletion != lastHostedCompletionIssued ||
            prepared.Registry is { } registry && !ReferenceEquals(registry.Original, hostedModelRegistry) ||
            !hostedBattlePresentation || hostedFrameServices is not null || hostedPresentationInvocation is not null ||
            hostedCompletionInvocation is not null && prepared.Acknowledgment is null ||
            !HostedControlPrefixRetired && (prepared.Acknowledgment is null || prepared.Controls != hostedControlPackets))
        {
            throw new InvalidOperationException("The prepared dispatch no longer belongs to this idle hosted process.");
        }
        if (prepared.Acknowledgment is { } ack)
        {
            ValidatePreparedHostedCompletionAck(ack);
        }
    }

    internal void CommitHostedDispatch(PreparedHostedDispatch prepared)
    {
        PublishHostedModelRegistry(prepared.Registry);
        nativeActionPackets = prepared.Packets;
        NativeGraphics = prepared.Admitted;
        hostedControlPackets = prepared.Controls;
        if (prepared.Action is { } action)
        {
            presentationSourceOwner = action.SourceOwner;
            presentationSourceResource = action.Source;
            presentationSourceModelId = action.ModelId;
            presentationSourceIsParty = action.IsParty;
            presentationPartyActorCount = action.PartyActorCount;
            hostedPresentationInvocation = action.Invocation;
            lastHostedActionIssued = action.Invocation.Token;
        }
        if (prepared.Acknowledgment is { } ack)
        {
            lastHostedCompletionAcknowledged = ack.Invocation.Request.Token;
        }
        hostedCompletionInvocation = prepared.Completion;
        if (prepared.Completion is { } completion)
        {
            lastHostedCompletionIssued = completion.Request.Token;
        }
        hostedPreparationEpoch = prepared.NextEpoch;
    }
}
