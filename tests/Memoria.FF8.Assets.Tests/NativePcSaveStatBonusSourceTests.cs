using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed partial class NativePcSaveConverterTests
{
    [Theory]
    [InlineData(false, true, 0, 0)]
    [InlineData(true, false, 0, 0)]
    [InlineData(true, true, 2970, 99)]
    public void Original_import_infers_level_growth_only_from_acquired_learned_bonus_abilities(bool acquired, bool learned, int expectedHp, int expectedStrength)
    {
        GameState source = Defaults();
        NativeCharacterVitals vitals = source.CharacterVitals!.DeepCopy();
        vitals.Records[0] = vitals.Records[0] with { HitPointBonus = 3100, StatBonuses = [120, 121, 122, 123, 124, 125] };
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Flags = acquired ? (byte)1 : (byte)0, LearnedBank = new(0, learned ? 6UL : 0UL) };
        source = source with { CharacterVitals = vitals };

        NativeCharacterVital imported = NativePcSaveConverter.Import(NativePcSaveConverter.Export(source), Defaults(), _ => "fixture", "en").CharacterVitals!.Records[0];

        NativeStatBonusSources sources = Assert.IsType<NativeStatBonusSources>(imported.BonusSources);
        NativeStatBonusGroup levels = Assert.Single(sources.Groups, group => group.Source == NativeStatBonusSources.LevelUp);
        NativeStatBonusGroup items = Assert.Single(sources.Groups, group => group.Source == NativeStatBonusSources.Items);
        Assert.Equal(expectedHp, levels.HitPoints);
        Assert.Equal(new[] { expectedStrength, 0, 0, 0, 0, 0 }, levels.Stats.ToArray());
        Assert.Equal(3100 - expectedHp, items.HitPoints);
        Assert.Equal(new[] { 120 - expectedStrength, 121, 122, 123, 124, 125 }, items.Stats.ToArray());
        Assert.Equal((ushort)3100, imported.HitPointBonus);
        Assert.Equal(new byte[] { 120, 121, 122, 123, 124, 125 }, imported.StatBonuses);
    }

    [Fact]
    public void Original_import_ignores_default_process_bonus_mode_before_clamping_native_hp()
    {
        GameState source = Defaults();
        NativeCharacterVitals vitals = source.CharacterVitals!.DeepCopy();
        vitals.Records[0] = vitals.Records[0] with { HitPointBonus = 2970, HitPoints = new(2000, 2000) };
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Flags = 1, LearnedBank = new(0, 2) };
        source = source with { CharacterVitals = vitals };
        GameState defaults = Defaults();
        defaults = defaults with { CharacterVitals = defaults.CharacterVitals! with { LevelBasedStatBonuses = true } };
        GameState imported = NativePcSaveConverter.Import(NativePcSaveConverter.Export(source), defaults, _ => "fixture", "en");
        Assert.False(imported.CharacterVitals!.LevelBasedStatBonuses);
        Assert.False(imported.GameplayPreferences.Tuning.LevelBasedStatBonuses);
        Assert.Equal((ushort)2000, imported.CharacterVitals.Records[0].HitPoints.Minimum);
    }

    [Fact]
    public void Original_export_sums_every_permanent_source_and_caps_native_words_with_level_mode_enabled()
    {
        GameState source = Defaults();
        NativeCharacterVitals vitals = source.CharacterVitals!.DeepCopy() with { LevelBasedStatBonuses = true };
        NativeStatBonusSources sources = new([
            new(NativeStatBonusSources.LevelUp, 2970, [99, 99, 99, 99, 0, 0]),
            new(NativeStatBonusSources.Items, 5000, [200, 10, 0, 0, 200, 0]),
            new(NativeStatBonusSources.Devour, 2500, [1, 2, 3, 4, 100, 6]),
            new("extension", 100, [1, 1, 1, 1, 1, 1])
        ]);
        vitals.Records[0] = vitals.Records[0].WithBonusSources(sources);
        source = source with { CharacterVitals = vitals };

        NativePcSaveData exported = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(source));
        int start = NativePcSaveData.MainOffset + 1088;

        Assert.Equal((ushort)9999, BinaryPrimitives.ReadUInt16LittleEndian(exported.Block.AsSpan(start + 2)));
        Assert.Equal(new byte[] { 255, 112, 103, 104, 255, 7 }, exported.Block.AsSpan(start + 10, 6).ToArray());
        Assert.True(vitals.Records[0].BonusSources!.EquivalentTo(sources));
    }
}
