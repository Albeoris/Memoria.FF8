using System.Collections.Immutable;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleModelStatusTimerOwnerTests
{
    [Fact]
    public void AuthenticatedColdImageProvidesAllSevenPhysicalStatusTimerRows()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources.");
        using GameAssets game = new(directory!);
        NativeBattleRetainedMemory retained = game.ReadColdProcessNativeBattleMemory();
        ImmutableArray<ImmutableArray<byte?>> rows = Assert.IsType<ImmutableArray<ImmutableArray<byte?>>>(retained.ModelStatusTimers);
        Assert.Equal(7, rows.Length);
        Assert.All(rows, row =>
        {
            Assert.Equal(32, row.Length);
            Assert.All(row, value => Assert.Equal((byte)0, value));
        });
    }
}
