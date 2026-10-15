using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleEnemyNativeStatsTests
{
    [Theory]
    [InlineData(110, 0, 0, 8)]
    [InlineData(110, 1, 0, 10)]
    [InlineData(111, 1, 0, 11)]
    [InlineData(200, 1, 0, 12)]
    [InlineData(201, 0, 0, 9)]
    [InlineData(250, 1, 0, 62)]
    [InlineData(251, 0, 3, 7)]
    [InlineData(251, 1, 3, 13)]
    [InlineData(252, 255, 0, 56)]
    [InlineData(253, 1, 24, 1)]
    [InlineData(253, 0, 15, 7)]
    public void OriginalLevelDispatcherPreservesCapsOffsetsAndBothRandomOperands(byte code, byte first, byte second, int expected)
    {
        Assert.Equal(expected, BattleEnemyNativeStats.ResolveLevel(code, [9, 10, 11], first, second));
    }

    [Fact]
    public void EverySceneLevelCodeHasExplicitEntropyOwnership()
    {
        for (int code = 0; code <= 255; code++)
        {
            int count = BattleEnemyNativeStats.LevelRandomByteCount((byte)code);
            Assert.Equal(code <= 100 || code == 254 ? 0 : code is 251 or 253 ? 2 : 1, count);
            Assert.InRange(BattleEnemyNativeStats.ResolveLevel((byte)code, [100], 255, 255), code == 0 ? 0 : 1, 100);
        }
        Assert.Throws<InvalidDataException>(() => BattleEnemyNativeStats.ResolveLevel(251, [10], 0));
        Assert.Throws<InvalidDataException>(() => BattleEnemyNativeStats.ResolveLevel(253, [10], 0));
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(1, 12)]
    [InlineData(2, 8)]
    [InlineData(255, 12)]
    public void SceneFfUsesOneBattleRandomParityAndTruncatedPartyAverage(byte roll, int expected)
    {
        Assert.Equal(expected, BattleEnemyNativeStats.ResolveLevel(255, [9, 10, 11], roll));
        Assert.Equal(10, BattleEnemyNativeStats.ResolveLevel(254, [9, 10, 11], roll));
    }

    [Fact]
    public void NativeRowsPreserveEveryIntegerDivisionAndElementScale()
    {
        BattleEnemyNativeStats stats = new(ImmutableArray.Create<byte>(2, 3, 4, 5), ImmutableArray.Create<byte>(10, 2, 40, 10, 2, 3, 20, 4, 20, 2, 60, 20, 3, 4, 30, 5, 4, 5, 40, 6, 5, 6, 50, 7), ImmutableArray.Create<short>(1000, 1000, 1000, 1000, 500, 1500, 0, 2000));
        BattleEnemyNativeProjection result = stats.Project(10);
        Assert.Equal(9060, result.MaximumHp);
        Assert.Equal(12, result.Strength);
        Assert.Equal(41, result.Vitality);
        Assert.Equal(20, result.Magic);
        Assert.Equal(60, result.Spirit);
        Assert.Equal(81, result.Speed);
        Assert.Equal(100, result.Evasion);
        Assert.Equal(stats.ElementDefenses, result.ElementDefenses);
    }

    [Theory]
    [InlineData(6, 256, 8, 14)]
    [InlineData(15, 300, 11, 3)]
    [InlineData(17, 294, 17, 7)]
    [InlineData(21, 368, 14, 42)]
    [InlineData(28, 154, 7, 4)]
    [InlineData(51, 13072, 33, 7)]
    public void InstalledBalambAndTrainingEnemiesProjectTheirActualCoefficientRows(int model, int maximumHp, int strength, int vitality)
    {
        using IFlArchive archive = FlArchive.OpenForRead(@"C:\Steam\steamapps\common\FINAL FANTASY VIII\Data\lang-en\battle.fs", FlArchiveRepresentation.Files);
        using Stream stream = archive.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1] == $"c0m{model:000}.dat").OpenForRead();
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        BattleEnemyNativeProjection result = BattleEnemyNativeStats.ReadMonster(bytes.ToArray()).Project(8);
        Assert.Equal(maximumHp, result.MaximumHp);
        Assert.Equal(strength, result.Strength);
        Assert.Equal(vitality, result.Vitality);
    }
}
