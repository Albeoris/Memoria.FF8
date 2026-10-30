using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSavedWorldTerrainTests
{
    [Fact]
    public void NewGameInitializesTheTerrainByteAndExecutedWritesPreserveOtherBits()
    {
        Assert.Equal(NativeWorldTerrainFlags109.Exact(0), NativeSavedWorldState.CreateNewGame().TerrainFlags109);
        NativeWorldTerrainFlags109 flags = NativeWorldTerrainFlags109.Exact(0xA4).WithEntranceFlag(true);
        Assert.Equal((byte)0xA5, flags.Value);
        Assert.Equal(byte.MaxValue, flags.KnownMask);
        Assert.True(flags.Matches(1));
        Assert.False(flags.Matches(0));
        Assert.False(flags.Matches(0x101));
        Assert.False(flags.Matches(ushort.MaxValue));
        Assert.Equal((byte)0xA4, flags.WithEntranceFlag(false).Value);
        Assert.Throws<InvalidDataException>(() => new NativeWorldTerrainFlags109(1, 0).Matches(1));
    }

    [Fact]
    public void LegacyUnknownBitsRemainUnknownAfterAnExecutedBitWrite()
    {
        NativeSavedWorldState legacy = NativeSavedWorldState.CreateNewGame() with { TerrainFlags109 = null };
        Assert.Throws<NotSupportedException>(() => legacy.MatchesTerrainFlags109(0));
        NativeSavedWorldState written = legacy.WithTerrainEntranceFlag(true);
        Assert.Equal((byte)1, written.TerrainFlags109!.KnownMask);
        Assert.True(written.MatchesTerrainFlags109(1));
        NativeSavedWorldState restored = JsonSerializer.Deserialize<NativeSavedWorldState>(JsonSerializer.Serialize(written))!;
        Assert.Equal(written.TerrainFlags109, restored.TerrainFlags109);
        Assert.Throws<NotSupportedException>(() => restored.WithTerrainEntranceFlag(null).MatchesTerrainFlags109(1));
    }

    [Fact]
    public void TerrainPublicationIsPartOfTheWorldTransaction()
    {
        GameState departure = new() { Scene = SceneKind.WorldMap, World = new(), SavedWorld = NativeSavedWorldState.CreateNewGame() };
        NativeWorldSalaryState batch = NativeWorldSalaryState.Begin(departure).WithTerrainEntranceFlag(true);
        GameState applied = batch.ApplyTo(departure, departure.World);
        Assert.True(applied.SavedWorld!.MatchesTerrainFlags109(1));
        Assert.True(departure.SavedWorld!.MatchesTerrainFlags109(0));
    }
}
