namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePresentationPartyRowsTests
{
    [Fact]
    public void EnemySourceDoesNotContributeAndNativeTargetsSupplyPartyRows()
    {
        Assert.Equal(new[] { 0, 1 }, NativeBattlePresentationPartyRows.Resolve(3, 20, null, null, [Target(1, 7), Target(0, 6)], [], 2));
        Assert.Equal(new[] { 1 }, NativeBattlePresentationPartyRows.Resolve(3, 20, null, null, [Target(1, 7)], [], 1));
    }

    [Fact]
    public void PlayerSourceNativeTargetAndForeignTargetContributeOnce()
    {
        Assert.Equal(new[] { 0, 1, 2 }, NativeBattlePresentationPartyRows.Resolve(0, 15, null, null,
            [Target(1, 0), Target(4, 16)], [Foreign(2, true), Foreign(3, false)], 3));
    }

    [Fact]
    public void ExplicitOwnershipUsesBoundPartyIdentityForTransformedModels()
    {
        Assert.Equal(new[] { 1, 3 }, NativeBattlePresentationPartyRows.Resolve(3, 22, true, null,
            [Target(1, 20, isParty: true), Target(0, 3, isParty: false)], [], 2));
        Assert.Equal(new[] { 3 }, NativeBattlePresentationPartyRows.Resolve(3, 22, null, GuardianFormInvocationMode.EntryOnly, [], [], 1));
        Assert.Throws<InvalidDataException>(() => NativeBattlePresentationPartyRows.Resolve(3, 22, false, GuardianFormInvocationMode.EntryOnly, [], [], 0));
    }

    [Fact]
    public void UnknownModelIdentityRequiresExplicitPartyMetadata()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattlePresentationPartyRows.Resolve(0, null, null, null, [], [], null));
        Assert.Throws<NotSupportedException>(() => NativeBattlePresentationPartyRows.Resolve(0, 1, null, null, [Target(1, null)], [], null));
        Assert.Equal(new[] { 0 }, NativeBattlePresentationPartyRows.Resolve(0, null, true, null, [Target(1, null, isParty: false)], [], 1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void RejectsOutOfBankBindingsEvenWhenTheyAreNotParty(int invalidRow)
    {
        Assert.Throws<InvalidDataException>(() => NativeBattlePresentationPartyRows.Resolve(invalidRow, 22, null, null, [], [], null));
        Assert.Throws<InvalidDataException>(() => NativeBattlePresentationPartyRows.Resolve(0, 0, null, null, [Target(invalidRow, 20)], [], null));
        Assert.Throws<InvalidDataException>(() => NativeBattlePresentationPartyRows.Resolve(0, 0, null, null, [], [Foreign(invalidRow, false)], null));
    }

    [Fact]
    public void RejectsOverlappingRowsBetweenAllOwners()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattlePresentationPartyRows.Resolve(0, 0, null, null, [Target(0, 1)], [], null));
        Assert.Throws<InvalidDataException>(() => NativeBattlePresentationPartyRows.Resolve(0, 0, null, null, [], [Foreign(0, true)], null));
        Assert.Throws<InvalidDataException>(() => NativeBattlePresentationPartyRows.Resolve(0, 0, null, null, [Target(1, 16)], [Foreign(1, false)], null));
        Assert.Throws<InvalidDataException>(() => NativeBattlePresentationPartyRows.Resolve(0, 0, null, null, [Target(1, 16), Target(1, 17)], [], null));
        Assert.Throws<InvalidDataException>(() => NativeBattlePresentationPartyRows.Resolve(0, 0, null, null, [], [Foreign(1, false), Foreign(1, true)], null));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void HostPartyCountMustMatchTheBoundPopulation(int expectedPartyCount)
    {
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => NativeBattlePresentationPartyRows.Resolve(0, 0, null, null, [], [Foreign(4, true)], expectedPartyCount));
        Assert.Equal("The native party presentation operation exceeds the host's available physical model bank.", exception.Message);
    }

    [Fact]
    public void UnhostedNativePresentationKeepsItsFixedThreeRows()
    {
        Assert.Equal(new[] { 0, 1, 2 }, NativeBattlePresentationPartyRows.Resolve(0, null, null, null, null, null, null));
    }

    private static NativeBattleEffectModelBinding Target(int index, byte? modelId, bool? isParty = null)
    {
        return new($"target-{index}", new(index, 0) { ModelId4 = modelId }, new([])) { IsParty = isParty };
    }

    private static NativeBattleForeignActor Foreign(int index, bool isParty)
    {
        return new(index, new(default, default, 0, 0, 4096), new(0, 0, 0, 0), (_, _) => default) { IsParty = isParty };
    }
}
