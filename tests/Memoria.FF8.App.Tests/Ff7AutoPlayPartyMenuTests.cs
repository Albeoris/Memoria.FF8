using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7AutoPlayPartyMenuTests
{
    [Fact]
    public void KeepsEligibleCurrentPartyAndDoesNotMutateSource()
    {
        Ff7Progress progress = Progress([2, 0, 1], 0x1FF);
        Assert.Equal([2, 0, 1], Ff7Scene.ChooseAutoPlayStoryParty(progress));
        Assert.Equal([2, 0, 1], progress.Party);
    }

    [Fact]
    public void FillsEmptySlotsInNativeCharacterOrderWithoutMovingLockedMember()
    {
        Ff7Progress progress = Progress([255, 2, 255], 0x1F, 1 << 2);
        Assert.Equal([0, 2, 1], Ff7Scene.ChooseAutoPlayStoryParty(progress));
    }

    [Fact]
    public void ReplacesDeadOrUnavailableUnlockedMembersAndSkipsLockedReserve()
    {
        Ff7Progress progress = Progress([0, 1, 2], 0x3D, 1 << 3, dead: [0]);
        Assert.Equal([4, 5, 2], Ff7Scene.ChooseAutoPlayStoryParty(progress));
    }

    [Fact]
    public void KeepsLockedDeadMemberInRequiredSlot()
    {
        Ff7Progress progress = Progress([0, 1, 2], 0xF, 1 << 1, dead: [1]);
        Assert.Equal([0, 1, 2], Ff7Scene.ChooseAutoPlayStoryParty(progress));
    }

    [Fact]
    public void UsesOriginalGuestVitalsInsteadOfResidentPlaceholderHp()
    {
        Ff7Progress progress = Progress([0, 1, 2], 0x1F, dead: [1]);
        Ff7GuestFieldVitalsView[] guests = [new(1, "Guest", 10, 100, 100), new(2, "Guest", 10, 0, 100)];
        Assert.Equal([0, 1, 3], Ff7Scene.ChooseAutoPlayStoryParty(progress, guests));
    }

    [Fact]
    public void FailsWithoutThreeEligibleMembersOrWithUnavailableLockedMember()
    {
        Assert.Throws<NotSupportedException>(() => Ff7Scene.ChooseAutoPlayStoryParty(Progress([0, 1, 255], 3)));
        Assert.Throws<NotSupportedException>(() => Ff7Scene.ChooseAutoPlayStoryParty(Progress([0, 1, 2], 0xD, 1 << 1)));
    }

    private static Ff7Progress Progress(int[] party, ushort available, ushort locked = 0, int[]? dead = null)
    {
        return new()
        {
            InitialKernelData = [],
            Characters = Enumerable.Range(0, 9).Select(id => new Ff7CharacterState(id, $"Character {id}", 1, 0, dead?.Contains(id) == true ? 0 : 100, 0, [])).ToArray(),
            Party = party,
            Inventory = [],
            Materia = [],
            AvailableCharacters = available,
            LockedCharacters = locked
        };
    }
}
