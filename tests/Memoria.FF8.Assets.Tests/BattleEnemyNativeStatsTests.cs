using System.Buffers.Binary;
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

    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 100)]
    [InlineData(100, 1)]
    [InlineData(199, 100)]
    [InlineData(255, 56)]
    public void UniformRandomLevelDoesNotRequireOrReadPartyLevels(byte roll, int expected)
    {
        Assert.Equal(expected, BattleEnemyNativeStats.ResolveLevel(252, [], roll));
        Assert.Equal(expected, BattleEnemyNativeStats.ResolveLevel(252, [0, 255], roll));
    }

    [Theory]
    [InlineData(254, 0, 0, 1)]
    [InlineData(255, 0, 0, 1)]
    [InlineData(255, 1, 0, 1)]
    [InlineData(251, 0, 3, 1)]
    [InlineData(253, 1, 255, 1)]
    public void MinimumPartyLevelPreservesNativeOneFloor(byte code, byte first, byte second, int expected)
    {
        Assert.Equal(expected, BattleEnemyNativeStats.ResolveLevel(code, [1], first, second));
    }

    [Theory]
    [InlineData(255, 0, 0, 80)]
    [InlineData(255, 1, 0, 100)]
    [InlineData(251, 0, 3, 65)]
    [InlineData(251, 1, 3, 65)]
    [InlineData(253, 1, 199, 99)]
    [InlineData(253, 0, 159, 79)]
    [InlineData(201, 1, 0, 100)]
    public void MaximumPartyLevelPreservesCapsAndExclusiveRemainder(byte code, byte first, byte second, int expected)
    {
        Assert.Equal(expected, BattleEnemyNativeStats.ResolveLevel(code, [100], first, second));
    }

    [Fact]
    public void PartyAverageTruncatesOnceBeforeVariationAndCountsEveryPresentMember()
    {
        Assert.Equal(10, BattleEnemyNativeStats.ResolveLevel(254, [9, 10, 13], 0));
        Assert.Equal(8, BattleEnemyNativeStats.ResolveLevel(255, [9, 10, 13], 0));
        Assert.Equal(12, BattleEnemyNativeStats.ResolveLevel(255, [9, 10, 13], 1));
        Assert.Equal(6, BattleEnemyNativeStats.ResolveLevel(254, [1, 12], 0));
        Assert.Equal(4, BattleEnemyNativeStats.ResolveLevel(254, [4], 0));
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
    [InlineData(2)]
    [InlineData(11)]
    public void OriginalDirectoriesSelectInformationRowsAndScaleElementBytes(int sectionCount)
    {
        const int information = 64;
        byte[] dat = new byte[information + 360];
        BinaryPrimitives.WriteInt32LittleEndian(dat, sectionCount);
        BinaryPrimitives.WriteInt32LittleEndian(dat.AsSpan(sectionCount == 11 ? 28 : 4), information);
        dat[information + 24] = 2;
        dat[information + 25] = 3;
        dat[information + 26] = 4;
        dat[information + 27] = 5;
        for (int index = 0; index < 24; index++)
        {
            dat[information + 28 + index] = (byte)(index + 1);
        }
        new byte[] { 0, 80, 90, 100, 120, 150, 200, 255 }.CopyTo(dat, information + 352);

        BattleEnemyNativeStats stats = BattleEnemyNativeStats.ReadMonster(dat);

        Assert.Equal(new byte[] { 2, 3, 4, 5 }, stats.HpCoefficients);
        Assert.Equal(Enumerable.Range(1, 24).Select(value => (byte)value), stats.StatCoefficients);
        Assert.Equal(new short[] { 0, 800, 900, 1000, 1200, 1500, 2000, 2550 }, stats.ElementDefenses);
        Assert.Equal(9060, stats.Project(10).MaximumHp);
    }

    [Fact]
    public void MaximumNativeCoefficientsPreserveDwordHpAndUpperByteStatCaps()
    {
        BattleEnemyNativeStats stats = new([255, 255, 255, 255], Enumerable.Repeat((byte)255, 24).ToImmutableArray(), Enumerable.Repeat((short)800, 8).ToImmutableArray());

        BattleEnemyNativeProjection result = stats.Project(100);

        Assert.Equal(2960550, result.MaximumHp);
        Assert.Equal(new[] { 255, 255, 255, 255, 255, 255 }, result.BaseStatistics);
        Assert.Equal(new[] { 255, 255, 255, 255, 255, 255 }, new[] { result.Strength, result.Vitality, result.Magic, result.Spirit, result.Speed, result.Evasion });
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
        using IFlArchive archive = FlArchive.OpenForRead(TestResources.GameFile("Data", "lang-en", "battle.fs"), FlArchiveRepresentation.Files);
        using Stream stream = archive.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1] == $"c0m{model:000}.dat").OpenForRead();
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        BattleEnemyNativeProjection result = BattleEnemyNativeStats.ReadMonster(bytes.ToArray()).Project(8);
        Assert.Equal(maximumHp, result.MaximumHp);
        Assert.Equal(strength, result.Strength);
        Assert.Equal(vitality, result.Vitality);
    }
}
