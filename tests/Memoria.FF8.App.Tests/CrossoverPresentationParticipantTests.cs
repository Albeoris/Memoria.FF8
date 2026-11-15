using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverPresentationParticipantTests
{
    [Fact]
    public void SixSelectedForeignTargetsRetainTheirHostSlotsAlongsideAnOptionalPartyModel()
    {
        CrossoverNativeMagicPresentationState state = State([4, 5, 6, 7, 8, 9], [1]);
        Assert.Equal(new[] { 4, 5, 6, 7, 8, 9 }, state.BoundContacts.Select(contact => contact.HostSlot));
        Assert.Equal(new[] { 4, 5, 6, 7, 8, 9, 1 }, state.BoundParticipants.Select(participant => participant.HostSlot));
        Assert.Equal(new byte?[] { 3, 4, 5, 6, null, null, 1 }, state.BoundParticipants.Select(participant => participant.Alias?.Offset));
        Assert.Equal(state.BoundTargets.Select(target => target.Contact.Recipient), state.BoundParticipants.Take(6).Select(participant => participant.Owner));
    }

    [Fact]
    public void OtherPartyParticipantsUseFreeSlotsIndependentlyFromSelectedContacts()
    {
        CrossoverNativeMagicPresentationState state = State([4, 5], [1, 2]);
        Assert.Equal(2, state.BoundTargets.Length);
        Assert.Equal(4, state.BoundParticipants.Length);
        Assert.Equal((byte?)1, Assert.Single(state.BoundParticipants, participant => participant.HostSlot == 1).Alias?.Offset);
        Assert.Equal((byte?)2, Assert.Single(state.BoundParticipants, participant => participant.HostSlot == 2).Alias?.Offset);
        Assert.Equal(new byte?[] { 3, 4 }, state.BoundParticipants.Take(2).Select(participant => participant.Alias?.Offset));
    }

    [Fact]
    public void ASelectedPartyMemberKeepsOneOwnerAndDoesNotCreateAnotherContactOrModel()
    {
        CrossoverNativeMagicPresentationState state = State([1, 4], [1, 2]);
        Assert.Equal(2, state.BoundTargets.Length);
        Assert.Equal(3, state.BoundParticipants.Length);
        CrossoverNativeMagicParticipant selected = Assert.Single(state.BoundParticipants, participant => participant.HostSlot == 1);
        Assert.Equal((byte?)1, selected.Alias?.Offset);
        Assert.Equal(state.BoundTargets[0].Contact.Recipient, selected.Owner);
        Assert.Equal((byte?)2, Assert.Single(state.BoundParticipants, participant => participant.HostSlot == 2).Alias?.Offset);
    }

    [Fact]
    public void SelectedTargetsBeyondTheOriginalEnemyAliasesKeepTheirTypedOwners()
    {
        int[] slots = [1, 4, 5, 6, 7, 8, 9];
        CrossoverNativeMagicPresentationState state = State(slots, []);
        Assert.Equal(slots, state.BoundTargets.Select(target => target.HostSlot));
        Assert.Equal(slots, state.BoundParticipants.Select(participant => participant.HostSlot));
        Assert.Equal(new byte?[] { 1, 3, 4, 5, 6, null, null }, state.BoundParticipants.Select(participant => participant.Alias?.Offset));
        Assert.Equal(state.BoundTargets.Select(target => target.Contact.Recipient), state.BoundParticipants.Select(participant => participant.Owner));
    }

    private static CrossoverNativeMagicPresentationState State(int[] targets, int[] otherParticipants)
    {
        ImmutableArray<CrossoverNativeMagicTarget> contacts = targets.Select(slot => new CrossoverNativeMagicTarget(slot, new(Owner(slot), 0, 0, 0, 0, 0, 0))).ToImmutableArray();
        ImmutableArray<CrossoverNativeMagicParticipant> participants = CrossoverNativeMagicParticipant.Bind(targets.Concat(otherParticipants), Owner);
        return new(null!, null!, null!, null!, null!, [], NativeCrtRandomState.FromColdProcess(), targets[0], contacts[0].Contact)
        {
            Targets = contacts,
            ParticipantBindings = participants
        };
    }

    private static BattleModelOwner Owner(int slot)
    {
        BattleForeignPresentationKey key = new($"host-slot-{slot}");
        return slot < 4 ? new BattleForeignPartyModel(key) : new BattleForeignEnemyModel(key);
    }
}
