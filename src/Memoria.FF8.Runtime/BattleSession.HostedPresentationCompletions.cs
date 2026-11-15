namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal sealed record HostedCompletionInvocation(NativeBattlePresentationCompletionRequested Request, int Slot, byte Serial, bool Reached = false, bool Retired = false);

    internal sealed class PreparedHostedCompletionAck(BattleSession owner, NativeBattleGraphicsState graphics, long epoch, long nextEpoch, HostedCompletionInvocation invocation, NativeBattleActionPacketsState originalPackets, NativeBattleActionPacketsState acknowledgedPackets)
    {
        internal BattleSession Owner { get; } = owner;
        internal NativeBattleGraphicsState Graphics { get; } = graphics;
        internal long Epoch { get; } = epoch;
        internal long NextEpoch { get; } = nextEpoch;
        internal HostedCompletionInvocation Invocation { get; } = invocation;
        internal NativeBattleActionPacketsState OriginalPackets { get; } = originalPackets;
        internal NativeBattleActionPacketsState AcknowledgedPackets { get; } = acknowledgedPackets;
    }

    private HostedCompletionInvocation? hostedCompletionInvocation;
    private long lastHostedCompletionIssued;
    private long lastHostedCompletionAcknowledged;

    internal NativeBattlePresentationCompletionRequested? PendingHostedCompletion => hostedCompletionInvocation is { Reached: true, Retired: true } pending ? pending.Request : null;

    internal bool IsHostedCompletionAcknowledged(long token)
    {
        if (token <= 0 || token > lastHostedCompletionIssued)
        {
            throw new ArgumentOutOfRangeException(nameof(token), "The completion token was not issued by this presentation process.");
        }
        return token <= lastHostedCompletionAcknowledged;
    }

    private static bool IsHostedCompletionCallback(uint callback)
    {
        return callback is 0x47DDA0 or 0x47DE70 or 0x47E120;
    }

    internal void RequireHostedCompletionAcknowledged()
    {
        if (PendingHostedCompletion is not null)
        {
            throw new InvalidOperationException("The retired hosted completion requires canonical acknowledgement before another frame.");
        }
    }

    internal PreparedHostedDispatch PrepareHostedCompletion(uint callback, BattleModelOwner owner, string actorId)
    {
        return PrepareHostedDispatch([], completion: new(callback, owner, actorId));
    }

    internal void ValidatePreparedHostedCompletion(PreparedHostedDispatch prepared)
    {
        ValidatePreparedHostedDispatch(prepared);
    }

    internal long PublishHostedCompletion(PreparedHostedDispatch prepared)
    {
        ValidatePreparedHostedDispatch(prepared);
        HostedCompletionInvocation completion = prepared.Completion ?? throw new InvalidDataException("Prepared dispatch has no completion.");
        CommitHostedDispatch(prepared);
        return completion.Request.Token;
    }

    private void ReachHostedCompletion(NativeBattleGraphicsState graphics, int slot, NativeBattleDescriptorPacket packet)
    {
        NativeBattleModelLifecycleServices services = hostedFrameServices ?? throw new InvalidOperationException("Hosted completion requires its current ordered receipt service.");
        if (hostedPresentationInvocation is not null || hostedCompletionInvocation is not { Reached: false } pending || !MatchesHostedCompletion(pending, slot, packet) ||
            !OwnsHostedCompletionActor(graphics, pending.Request))
        {
            throw new InvalidDataException("The native combat completion is not owned by this hosted descriptor.");
        }

        services.EmitCompletion(pending.Request);
        hostedCompletionInvocation = pending with { Reached = true };
    }

    private void ObserveHostedCompletionRemoved(NativeBattleGraphicsState graphics, NativeBattleDescriptorState state, int slot, NativeBattleDescriptorPacket packet)
    {
        if (hostedCompletionInvocation is { Reached: true, Retired: false } pending && MatchesHostedCompletion(pending, slot, packet))
        {
            // Preserve the observed retirement even if a later frame operation fails.
            NativeGraphics = graphics with { Descriptors = state };
            hostedCompletionInvocation = pending with { Retired = true };
        }
    }

    internal bool IsHostedCompletionQueued(long token)
    {
        if (hostedCompletionInvocation is not { Reached: false, Retired: false } pending || pending.Request.Token != token ||
            NativeGraphics is not { } graphics || !OwnsHostedCompletionActor(graphics, pending.Request))
        {
            return false;
        }
        return graphics.Descriptors.Queue.Contains(pending.Slot) && MatchesHostedCompletion(pending, pending.Slot, graphics.Descriptors.Packets[pending.Slot]);
    }

    private static bool OwnsHostedCompletionActor(NativeBattleGraphicsState graphics, NativeBattlePresentationCompletionRequested request)
    {
        return graphics.ActorModels.Contains(request.Owner) && StringComparer.Ordinal.Equals(RequireCanonicalGraphicsActorId(request.Owner), request.ActorId);
    }

    private static bool MatchesHostedCompletion(HostedCompletionInvocation pending, int slot, NativeBattleDescriptorPacket packet)
    {
        return slot == pending.Slot && packet.Serial == pending.Serial && packet.Command == 10 && packet.Argument == pending.Request.Callback && packet.PriorityState == 128;
    }

    internal PreparedHostedCompletionAck PrepareHostedCompletionAck(long token, bool resetCounts)
    {
        if (!hostedBattlePresentation || hostedPresentationInvocation is not null ||
            hostedCompletionInvocation is not { Reached: true, Retired: true } pending || pending.Request.Token != token ||
            hostedFrameServices is not null || NativeGraphics is not { EntryReady: true } graphics)
        {
            throw new InvalidOperationException("Acknowledgement requires the exact reached and retired hosted completion.");
        }
        if (!OwnsHostedCompletionActor(graphics, pending.Request))
        {
            throw new InvalidDataException("Hosted completion acknowledgement requires its canonical bound actor.");
        }
        if (!resetCounts && pending.Request.Callback != 0x47DE70)
        {
            throw new InvalidDataException("Only native47DE70 can retain its action counts for follow-up work.");
        }
        NativeBattleActionPacketsState original = nativeActionPackets;
        NativeBattleActionPacketsState acknowledged = resetCounts ? original.ResetCounts() : original;
        return new(this, graphics, hostedPreparationEpoch, checked(hostedPreparationEpoch + 1), pending, original, acknowledged);
    }

    internal void ValidatePreparedHostedCompletionAck(PreparedHostedCompletionAck prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (!ReferenceEquals(prepared.Owner, this) || !ReferenceEquals(prepared.Graphics, NativeGraphics) || prepared.Epoch != hostedPreparationEpoch ||
            !ReferenceEquals(prepared.Invocation, hostedCompletionInvocation) || !ReferenceEquals(prepared.OriginalPackets, nativeActionPackets) ||
            !hostedBattlePresentation || hostedPresentationInvocation is not null || hostedFrameServices is not null)
        {
            throw new InvalidOperationException("The prepared acknowledgement no longer belongs to this retired hosted completion.");
        }
    }

    internal void PublishHostedCompletionAck(PreparedHostedCompletionAck prepared)
    {
        PreparedHostedDispatch acknowledgment = PrepareHostedDispatch([], acknowledgment: prepared);
        CommitHostedDispatch(acknowledgment);
    }
}
