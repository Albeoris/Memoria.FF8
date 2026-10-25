using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticLevelPreparationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Initial_level_requires_a_native_level_identity(int level)
    {
        DiagnosticBattlePreparation fixture = new([], [new(0, 0, [], []) { Level = level }], false);
        Assert.Throws<InvalidDataException>(fixture.Validate);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(100)]
    public void Installed_level_preparation_awards_native_experience_and_rebuilds_before_explicit_hp(int level)
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals();
        NativeBattleExperienceDefinition experience = assets.ReadNativeBattleExperienceDefinition();
        byte row = original.CombatParty.First(value => value != 255);
        JsonElement before = JsonSerializer.SerializeToElement(original);
        NativeCharacterVitals leveled = GameWorld.PrepareDiagnosticCharacterLevel(original, row, level, experience);
        ushort maximum = leveled.GetMagicMaximumHitPoints(row);
        DiagnosticBattlePreparation fixture = new([], [new(row, original.Records[row].CharacterId, [], []) { Level = level, HitPoints = maximum }], false);
        DiagnosticPreparationResult result = GameWorld.PrepareDiagnosticCharacters(original, fixture, experience);
        Assert.Equal(level, NativeStatDefinitions.Level(result.Vitals.Records[row].Experience, original.Definitions.Characters[original.Records[row].CharacterId]));
        Assert.Equal(maximum, result.Vitals.Records[row].HitPoints.Minimum);
        Assert.Equal(maximum, result.Vitals.Records[row].HitPoints.Maximum);
        Assert.True(result.Vitals.Records[row].Experience > original.Records[row].Experience);
        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(original)));
        Assert.Contains(result.Changes, change => change.Owner == "character" && change.Row == row);
        Assert.True(original.GuardianForces.Select(gf => gf.Experience).SequenceEqual(result.Vitals.GuardianForces.Select(gf => gf.Experience)));
    }

    [Fact]
    public void Installed_level_preparation_rejects_lowering_and_missing_or_mismatched_resources()
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals();
        NativeBattleExperienceDefinition experience = assets.ReadNativeBattleExperienceDefinition();
        byte row = original.CombatParty.First(value => value != 255);
        int current = NativeStatDefinitions.Level(original.Records[row].Experience, original.Definitions.Characters[original.Records[row].CharacterId]);
        Assert.True(current > 1);
        JsonElement before = JsonSerializer.SerializeToElement(original);
        Assert.Throws<NotSupportedException>(() => GameWorld.PrepareDiagnosticCharacterLevel(original, row, current - 1, experience));
        DiagnosticBattlePreparation fixture = new([], [new(row, original.Records[row].CharacterId, [], []) { Level = 100 }], false);
        Assert.Throws<NotSupportedException>(() => GameWorld.PrepareDiagnosticCharacters(original, fixture));
        Assert.Throws<InvalidDataException>(() => GameWorld.PrepareDiagnosticCharacters(original, fixture, experience with { KernelSha256 = new string('0', 64) }));
        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(original)));
    }

    private static GameAssets InstalledAssets()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for native level preparation validation.");
        return new(directory);
    }
}
