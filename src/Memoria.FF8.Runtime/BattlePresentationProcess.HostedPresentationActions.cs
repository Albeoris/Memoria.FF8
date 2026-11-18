using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal enum BattlePresentationInvocationProvenance
{
    Gameplay,
    ExternalDiagnostic
}

public sealed partial class BattlePresentationProcess
{
    internal sealed record HostedPresentationInvocation(long Token, int DescriptorSlot, byte DescriptorSerial, BattleActionHeaderReference Header)
    {
        internal BattlePresentationInvocationProvenance Provenance { get; init; }
        internal bool AwaitingRenzokukenWidgetClose { get; init; }
        internal bool RenzokukenWidgetClosed { get; init; }
        internal bool DiagnosticOutcomeEntryIssued { get; init; }
        internal bool DiagnosticOutcomeEntryAcknowledged { get; init; }
    }

    private HostedPresentationInvocation? hostedPresentationInvocation;
    private long lastHostedActionIssued;
    private long lastHostedActionCompleted;
    private long hostedPreparationEpoch;

    internal long? HostedActiveActionToken => hostedPresentationInvocation?.Token;

    internal bool IsHostedActionComplete(long token)
    {
        if (token <= 0 || token > lastHostedActionIssued)
        {
            throw new ArgumentOutOfRangeException(nameof(token), "The action token was not issued by this presentation process.");
        }
        return token <= lastHostedActionCompleted;
    }

    internal long BeginHostedAction(NativeBattleActionHeaderPreparation header, ImmutableArray<NativeBattleContactPacket> contacts)
    {
        return PublishHostedAction(PrepareHostedAction(header, contacts));
    }

    internal PreparedHostedDispatch PrepareHostedAction(NativeBattleActionHeaderPreparation header, ImmutableArray<NativeBattleContactPacket> contacts)
    {
        return PrepareHostedDispatch([new(header, contacts)], dispatchAppended: true, freshActionPackets: true);
    }

    internal void ValidatePreparedHostedAction(PreparedHostedDispatch prepared)
    {
        ValidatePreparedHostedDispatch(prepared);
    }

    internal long PublishHostedAction(PreparedHostedDispatch prepared)
    {
        ValidatePreparedHostedDispatch(prepared);
        HostedDispatchAction action = prepared.Action ?? throw new InvalidDataException("Prepared dispatch has no action.");
        CommitHostedDispatch(prepared);
        return action.Invocation.Token;
    }

    private void ObserveHostedActionPacketRemoved(int slot, NativeBattleDescriptorPacket packet)
    {
        if (hostedPresentationInvocation is { } invocation && slot == invocation.DescriptorSlot &&
            packet.Serial == invocation.DescriptorSerial && packet.Command == 104 && packet.Header == invocation.Header &&
            packet.PriorityState == 255)
        {
            lastHostedActionCompleted = invocation.Token;
            hostedPresentationInvocation = null;
        }
    }
}
