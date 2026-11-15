using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverPresentationParticipantTests
{
    [Fact]
    public void SixSelectedForeignTargetsRetainTheirPhysicalSlotsWithoutAcquiringAnOptionalPartyContact()
    {
        var targets = CrossoverNativeMagicTarget.Bind(0, [4, 5, 6, 7, 8, 9], (_, index) => new(index, 0, 0, 0, 0, 0, 0));
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, targets.Select(target => (int)target.Contact.ActorIndex));
        CrossoverNativeMagicPresentationState state = State() with { Targets = targets, Participants = [1] };
        Assert.Equal(6, state.BoundParticipants.Length);
        Assert.DoesNotContain(state.BoundParticipants, participant => participant.HostSlot == 1);
        Assert.Equal(targets.Select(target => (target.HostSlot, (int)target.Contact.ActorIndex)), state.BoundParticipants.Select(participant => (participant.HostSlot, participant.ActorIndex)));
    }

    [Fact]
    public void OtherPartyParticipantsUseFreeSlotsIndependentlyFromSelectedContacts()
    {
        var targets = CrossoverNativeMagicTarget.Bind(0, [4, 5], (_, index) => new(index, 0, 0, 0, 0, 0, 0));
        CrossoverNativeMagicPresentationState state = State() with { Targets = targets, Participants = [1, 2] };
        Assert.Equal(2, state.BoundTargets.Length);
        Assert.Equal(4, state.BoundParticipants.Length);
        Assert.Equal(1, Assert.Single(state.BoundParticipants, participant => participant.HostSlot == 1).ActorIndex);
        Assert.Equal(2, Assert.Single(state.BoundParticipants, participant => participant.HostSlot == 2).ActorIndex);
        Assert.Equal(new byte[] { 3, 4 }, state.BoundTargets.Select(target => target.Contact.ActorIndex));
    }

    [Fact]
    public void ASelectedPartyMemberKeepsOneOwnerAndDoesNotCreateAnotherContactOrModel()
    {
        var targets = CrossoverNativeMagicTarget.Bind(0, [1, 4], (_, index) => new(index, 0, 0, 0, 0, 0, 0));
        CrossoverNativeMagicPresentationState state = State() with { Targets = targets, Participants = [1, 2] };
        Assert.Equal(2, state.BoundTargets.Length);
        Assert.Equal(3, state.BoundParticipants.Length);
        Assert.Equal(3, Assert.Single(state.BoundParticipants, participant => participant.HostSlot == 1).ActorIndex);
        Assert.Equal(1, Assert.Single(state.BoundParticipants, participant => participant.HostSlot == 2).ActorIndex);
    }

    [Fact]
    public void SelectedTargetsStillRejectActualModelBankOverflow()
    {
        Assert.Throws<NotSupportedException>(() => CrossoverNativeMagicTarget.Bind(0, [1, 4, 5, 6, 7, 8, 9], (_, index) => new(index, 0, 0, 0, 0, 0, 0)));
    }

    private static CrossoverNativeMagicPresentationState State() => new(null!, null!, null!, null!, null!, [], NativeCrtRandomState.FromColdProcess(), 4, new(OriginalOwner(3), 0, 0, 0, 0, 0, 0));
}
