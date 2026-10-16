using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeEncounterTablesTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Fact]
    public void InstalledExecutableOwnsBothOriginalRandomTablesAndFormationWeights()
    {
        using GameAssets assets = new(GameDirectory);
        NativeEncounterTables tables = assets.ReadNativeEncounterTables();
        Assert.Equal(tables.FieldRandom.ToArray(), tables.WorldRandom.ToArray());
        Assert.Equal(new byte[] { 7, 182, 240, 31, 85, 91, 55, 227 }, tables.FieldRandom.Take(8));
        Assert.Equal(new byte[] { 37, 37, 37, 37, 36, 36, 24, 12 }, tables.WorldWeights);
    }

    [Theory]
    [InlineData(6, 2, new ushort[] { 514, 514, 514, 512, 512, 520, 513, 513 })]
    [InlineData(4, 8, new ushort[] { 515, 515, 515, 515, 516, 516, 521, 521 })]
    [InlineData(10, 2, new ushort[] { 517, 517, 517, 517, 517, 517, 517, 517 })]
    [InlineData(24, 2, new ushort[] { 518, 518, 518, 518, 518, 519, 519, 519 })]
    public void BalambRegionZeroUsesOriginalGroundSpecificFormations(byte ground, byte rate, ushort[] formations)
    {
        using WorldAssets assets = new(GameDirectory);
        var definition = assets.ReadRandomEncounterDefinition(24576, -29406, ground, 1)!;
        Assert.Equal(rate, definition.Rate);
        Assert.Equal(formations, definition.Formations);
    }

    [Fact]
    public void FieldRatAndMrtReaderUsesOnlyTheReachedNativeOperands()
    {
        var definition = NativeEncounterTables.ReadField([4, 4, 4, 4], [34, 0, 34, 0, 37, 0, 36, 0]);
        Assert.Equal((byte)4, definition.Rate);
        Assert.Equal(new ushort[] { 34, 34, 37, 36 }, definition.Formations);
    }

    [Fact]
    public void InstalledFieldLoadingRetainsItsRealRateAndFourFormations()
    {
        using GameAssets assets = new(GameDirectory);
        var gate = assets.LoadField(159).RandomEncounters!;
        Assert.Equal((byte)4, gate.Rate);
        Assert.Equal(new ushort[] { 34, 34, 37, 36 }, gate.Formations);
        Assert.Equal((byte)0, assets.LoadField(229).RandomEncounters!.Rate);
    }
}
