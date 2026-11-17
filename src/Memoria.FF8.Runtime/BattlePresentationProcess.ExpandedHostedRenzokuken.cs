using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    internal sealed record HostedRenzokukenFinisherReplacement(NativeBattleRenzokukenFinisherReached Receipt, NativeBattleActionPacketsState Packets, BattleContactCursor FirstContact, ImmutableArray<NativeBattleContactPacket> Contacts, ImmutableArray<NativeBattleContactPacket> Routed);

    private NativeBattleRenzokukenFinisherReached? pendingHostedRenzokukenFinisher;
    private NativeBattleRenzokukenWidgetStepped? hostedRenzokukenPendingStep;
    private NativeBattleRenzokukenWidgetStepped? hostedRenzokukenPublishedStep;
    private HostedRenzokukenFrame? hostedRenzokukenUiFrame;
    private ImmutableArray<NativeBattleRenzokukenWidgetStepped>.Builder? hostedRenzokukenUiSteps;

    private void BeginHostedRenzokukenUi(HostedRenzokukenFrame? frame)
    {
        hostedRenzokukenUiFrame = frame;
        hostedRenzokukenUiSteps = frame is null ? null : ImmutableArray.CreateBuilder<NativeBattleRenzokukenWidgetStepped>();
    }

    private void DrainHostedRenzokukenUi(Action<ImmutableArray<NativeBattleRenzokukenWidgetStepped>, long>? publish)
    {
        ImmutableArray<NativeBattleRenzokukenWidgetStepped>.Builder? steps = hostedRenzokukenUiSteps;
        hostedRenzokukenUiFrame = null;
        hostedRenzokukenUiSteps = null;
        if (steps is { Count: > 0 })
        {
            (publish ?? throw new InvalidOperationException("Hosted Renzokuken UI requires its canonical input handoff."))(steps.ToImmutable(), hostedPreparationEpoch);
        }
    }

    private void RecordHostedRenzokukenUi(NativeBattleTriggerWidgetStep step, NativeBattleTriggerReceipt? pending)
    {
        if (hostedRenzokukenUiFrame is not { } frame || hostedRenzokukenUiSteps is not { } steps ||
            hostedPresentationInvocation is not { } invocation || invocation.Token != frame.ActionToken ||
            nativeActionPackets.ReadHeader(invocation.Header) is not { Command: 5 } header || header.Source != frame.Owner.Actor ||
            step.Widget.Actor != frame.Owner.Actor || step.Widget.SourceMagicId != frame.Owner.MagicId || header.Effect != frame.Owner.MagicId ||
            pending is not null && hostedRenzokukenPendingStep is not null)
        {
            throw new InvalidDataException("Native Renzokuken UI stepped outside its actual hosted input owner or overwrote a pending trigger.");
        }
        NativeBattleRenzokukenWidgetStepped receipt = new(frame, step.Widget, pending, step.Unregistered, hostedPreparationEpoch, steps.Count);
        steps.Add(receipt);
        if (pending is not null)
        {
            hostedRenzokukenPendingStep = receipt;
        }
        if (step.Unregistered)
        {
            hostedPresentationInvocation = invocation with { RenzokukenWidgetClosed = true };
        }
    }

    private HostedRenzokukenContact? ReadHostedRenzokukenContact(NativeBattleContactPacket packet)
    {
        if (packet.Occurrence is null || hostedPresentationInvocation is not { } invocation || nativeActionPackets.ReadHeader(invocation.Header).Command != 5)
        {
            return null;
        }
        if (hostedRenzokukenPublishedStep is not { Pending: { } trigger } step || step.Frame.ActionToken != invocation.Token)
        {
            throw new InvalidDataException("Hosted Renzokuken published a contact without its actual recomputed trigger.");
        }
        return new(step.Frame.Owner, invocation.Token, trigger.ContactIndex);
    }

    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollNativeRenzokukenAction104(NativeBattleGraphicsState graphics, int slot, NativeBattleDescriptorPacket packet, byte phase)
    {
        if (hostedPresentationInvocation is not { } invocation || hostedFrameServices?.Renzokuken is not { } frame ||
            invocation.Token != frame.ActionToken || invocation.DescriptorSlot != slot || invocation.DescriptorSerial != packet.Serial ||
            packet.Header != invocation.Header || nativeActionPackets.ReadHeader(invocation.Header) is not { Command: 5 } header ||
            header.Source != frame.Owner.Actor || header.Effect != frame.Owner.MagicId)
        {
            if (hostedPresentationInvocation is { AwaitingRenzokukenWidgetClose: true, DescriptorSlot: var retained } && retained == slot)
            {
                throw new InvalidDataException("The closing Renzokuken widget lost its hosted action owner.");
            }
            return PollNativeActionGraphics(graphics, phase);
        }

        bool complete = invocation.AwaitingRenzokukenWidgetClose;
        if (!complete)
        {
            (graphics, phase, complete) = PollNativeActionGraphics(graphics, phase);
        }
        if (!complete)
        {
            return (graphics, phase, false);
        }
        if (graphics.TriggerWidget is not { } widget)
        {
            if (invocation.AwaitingRenzokukenWidgetClose && !invocation.RenzokukenWidgetClosed)
            {
                throw new InvalidDataException("The completed native Renzokuken action lost its closing widget without an unregister callback.");
            }
            return (graphics, phase, true);
        }
        if (widget.Actor != frame.Owner.Actor || widget.SourceMagicId != frame.Owner.MagicId ||
            widget.Input.Phase != 8 || frame.State.Input.Phase != 8 || widget.Input.ContactIndex != frame.Contacts ||
            frame.Contacts != widget.Block.Timeline.InputThresholds.Length || graphics.PendingTrigger is not null)
        {
            throw new InvalidDataException("The completed native Renzokuken action does not own a widget closing after its last contact.");
        }
        if (!invocation.AwaitingRenzokukenWidgetClose)
        {
            hostedPresentationInvocation = invocation with { AwaitingRenzokukenWidgetClose = true };
        }
        return (graphics, phase, false);
    }

    internal NativeBattleRenzokukenFinisherReached? PendingHostedRenzokukenFinisher => pendingHostedRenzokukenFinisher;

    private NativeBattleGraphicsState RunExpandedHostedRenzokukenFinisher(NativeBattleGraphicsState graphics, uint callback)
    {
        NativeBattleModelLifecycleServices services = hostedFrameServices ?? throw new InvalidOperationException("Hosted Renzokuken formula requires its current frame facts.");
        int slot = graphics.Descriptors.Queue[0];
        byte serial = graphics.Descriptors.Packets[slot].Serial;
        if (services.Renzokuken is not { } frame || frame.Owner.FinisherCallback != callback ||
            pendingHostedRenzokukenFinisher is not null ||
            hostedControlPackets.SingleOrDefault(control => !control.Retired && control.Slot == slot && control.Serial == serial && control.Command == 10 &&
                control.Callback == callback && ReferenceEquals(control.Renzokuken, frame.Owner))?.RenzokukenHeader is not BattleActionHeaderReference header)
        {
            throw new InvalidDataException("Native Renzokuken formula does not identify its reached finisher control and command250 header.");
        }
        NativeBattleRenzokukenFinisherReached receipt = new(frame.Owner, header, slot, serial);
        pendingHostedRenzokukenFinisher = receipt;
        services.Emit(receipt);
        return graphics;
    }
}
