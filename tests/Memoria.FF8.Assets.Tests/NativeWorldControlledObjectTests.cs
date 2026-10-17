using System.Collections.Immutable;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeWorldControlledObjectTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 4)]
    [InlineData(2, 5)]
    [InlineData(3, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 0)]
    [InlineData(6, 6)]
    [InlineData(7, 0)]
    [InlineData(8, 0)]
    [InlineData(9, 0)]
    public void FootSelectorUsesTheOriginalRequestedEntityToShapeMapping(byte entityType, byte shape)
    {
        Assert.True(Clause(shape).HasRequestedFootObject(entityType));
        Assert.False(Clause(shape == 0 ? (byte)4 : (byte)0).HasRequestedFootObject(entityType));
        Assert.False(Clause(73, 74, 79, 80, 94).HasRequestedFootObject(entityType));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(48)]
    [InlineData(50)]
    [InlineData(128)]
    [InlineData(132)]
    [InlineData(255)]
    public void OtherEntityModesRemainUndecidedRatherThanBeingDeclaredAbsent(byte entityType)
    {
        Assert.Null(Clause(0, 4, 5, 6).HasRequestedFootObject(entityType));
    }

    [Fact]
    public void InstalledClauseLookupUsesActualResourcesAndCachesWithoutBuildingAWorldScene()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeWorldAllocationClause clause = assets.ReadNativeWorldAllocationClause(40);
        Assert.False(clause.HasRequestedFootObject(0));
        Assert.True(clause.HasRequestedFootObject(1));
        Assert.Same(clause, assets.ReadNativeWorldAllocationClause(40));
        Assert.True(assets.ReadNativeWorldAllocationClause(17).HasRequestedFootObject(0));
    }

    private static NativeWorldAllocationClause Clause(params byte[] shapes)
    {
        ImmutableArray<ushort>.Builder words = ImmutableArray.CreateBuilder<ushort>();
        words.AddRange(new ushort[] { 0xFF01, 0, 0xFF02, 123, 0xFF03, 456, 0xFF04, 0 });
        foreach (byte shape in shapes)
        {
            words.Add(0xFF13);
            words.Add(shape);
        }
        words.AddRange(new ushort[] { 0xFF05, 0 });
        ImmutableArray<ushort> program = words.ToImmutable();
        return new NativeWorldAllocationClause(7, 84, program.Length * 2, 123, 456, program);
    }
}
