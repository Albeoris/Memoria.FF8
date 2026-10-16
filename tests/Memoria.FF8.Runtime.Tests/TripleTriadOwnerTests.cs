using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class TripleTriadOwnerTests
{
    [Theory]
    [InlineData(1523, 1)]
    [InlineData(1524, 2)]
    [InlineData(1526, 2)]
    [InlineData(1528, 2)]
    [InlineData(1532, 4)]
    public void CanonicalFlagCounterAndRandomOwnersRejectGenericPublicationAtomically(int address, int width)
    {
        GameSession session = new(new GameState { CardInventory = NativeCardInventory.CreateNewGame() });
        GameState before = session.State;
        IReadOnlyList<NativeEventMemoryRegion> regions = NativeEventMemoryOwnership.RootRegions(before);
        Assert.Equal(NativeEventMemoryOwner.Cards, NativeEventMemoryOwnership.Resolve(address, width, regions));
        NativeEventMemory overlapping = NativeEventMemory.Empty.Write(address, width, NativeScalarValue.Exact(7));
        Assert.Throws<InvalidDataException>(() => session.SetEventMemory(overlapping));
        Assert.Same(before, session.State);
        Assert.Empty(session.State.EventMemory.Cells);
    }

    [Theory]
    [InlineData(1522, 2)]
    [InlineData(1523, 2)]
    [InlineData(1528, 4)]
    [InlineData(1530, 4)]
    [InlineData(1531, 2)]
    public void AccessAcrossCardModuleStorageBoundariesNeverChoosesARepresentativeOwner(int address, int width)
    {
        IReadOnlyList<NativeEventMemoryRegion> regions = NativeEventMemoryOwnership.RootRegions(new GameState { CardInventory = NativeCardInventory.CreateNewGame() });
        Assert.Throws<NotSupportedException>(() => NativeEventMemoryOwnership.Resolve(address, width, regions));
    }

    [Fact]
    public void ReservedWord1530RemainsGenericBetweenStatisticsAndRandomOwners()
    {
        GameSession session = new(new GameState { CardInventory = NativeCardInventory.CreateNewGame() });
        Assert.Equal(NativeEventMemoryOwner.Generic, NativeEventMemoryOwnership.Resolve(1530, 2, NativeEventMemoryOwnership.RootRegions(session.State)));
        NativeEventMemory reserved = NativeEventMemory.Empty.Write(1530, 2, NativeScalarValue.Exact(0xABCD));
        session.SetEventMemory(reserved);
        Assert.Equal(0xABCD, session.State.EventMemory.Read(1530, 2).RequireExact());
        Assert.Equal(1u, session.CardInventory!.TripleTriadRandomState);
        Assert.Equal(new TripleTriadStatistics(0, 0, 0), session.CardInventory.TripleTriadStatistics);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void NullableMissingModuleOwnersDoNotClaimBytesOrInventSavedValues(int mask)
    {
        NativeCardInventory inventory = NativeCardInventory.CreateNewGame() with
        {
            CollectionFlags = (mask & 1) != 0 ? (byte)0 : null,
            TripleTriadStatistics = (mask & 2) != 0 ? new TripleTriadStatistics(0, 0, 0) : null,
            TripleTriadRandomState = (mask & 4) != 0 ? 1u : null
        };
        GameSession session = new(new GameState { CardInventory = inventory });
        IReadOnlyList<NativeEventMemoryRegion> regions = NativeEventMemoryOwnership.RootRegions(session.State);
        NativeEventMemory generic = NativeEventMemory.Empty;
        foreach ((int bit, int address, int width) in new[] { (1, 1523, 1), (2, 1524, 2), (2, 1526, 2), (2, 1528, 2), (4, 1532, 4) })
        {
            Assert.Equal((mask & bit) != 0 ? NativeEventMemoryOwner.Cards : NativeEventMemoryOwner.Generic, NativeEventMemoryOwnership.Resolve(address, width, regions));
            if ((mask & bit) == 0)
            {
                generic = generic.Write(address, width, NativeScalarValue.Exact(17));
            }
        }
        session.SetEventMemory(generic);
        Assert.Equal(inventory.CollectionFlags, session.CardInventory!.CollectionFlags);
        Assert.Equal(inventory.TripleTriadStatistics, session.CardInventory.TripleTriadStatistics);
        Assert.Equal(inventory.TripleTriadRandomState, session.CardInventory.TripleTriadRandomState);
        Assert.Equal(NativeEventMemoryOwner.Cards, NativeEventMemoryOwnership.Resolve(1408, 1, regions));
    }
}
