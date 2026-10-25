using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticCharacterPreparationTests
{
    [Fact]
    public void Preparation_requires_explicit_collections_and_scheduling()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DiagnosticBattlePreparation>("{}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DiagnosticCharacterPreparation>("{}"));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(64, 2)]
    [InlineData(21, 1)]
    [InlineData(1, 101)]
    public void Preparation_rejects_unrelated_actions_and_insufficient_or_excessive_stock(int action, int count)
    {
        DiagnosticBattlePreparation recipe = new([], [new(0, 0, [new(action, count)], [])], false);
        Assert.Throws<InvalidDataException>(recipe.Validate);
    }

    [Fact]
    public void Preparation_accepts_native_magic_identities_without_an_effect_allowlist()
    {
        DiagnosticBattlePreparation recipe = new([], [new(0, 0, Enumerable.Range(10, 32).Select(action => new DiagnosticMagicStockPreparation(action, 2)).ToImmutableArray(), [])], false);
        recipe.Validate();
    }

    [Fact]
    public void Installed_preparation_uses_saved_records_and_learned_native_magic_command()
    {
        NativeCharacterVitals original = InstalledNewGame();
        byte row = original.CombatParty.First(value => value != 255);
        byte character = original.Records[row].CharacterId;
        int ability = Enumerable.Range(20, 19).Single(value => original.MenuDefinitions.CommandAbilityCommands[value - 20] == 2);
        int gf = Enumerable.Range(0, 16).First(id => original.GuardianForceAbilities(id).Any(entry => entry.Ability == ability && entry.Learned));
        ushort hp = checked((ushort)Math.Max(1, original.GetMagicMaximumHitPoints(row) / 2));
        DiagnosticBattlePreparation recipe = new([new(gf, true)], [new(row, character, [new(21, 2), new(10, 2), new(22, 2)], [gf]) { MagicCommand = new(0, ability), HitPoints = hp }], true);
        JsonElement source = JsonSerializer.SerializeToElement(original);

        DiagnosticPreparationResult result = GameWorld.PrepareDiagnosticCharacters(original, recipe);

        Assert.True(JsonElement.DeepEquals(source, JsonSerializer.SerializeToElement(original)));
        Assert.Equal(hp, result.Vitals.Records[row].HitPoints.Minimum);
        Assert.Equal(hp, result.Vitals.Records[row].HitPoints.Maximum);
        Assert.Equal(ability, (byte)result.Vitals.Records[row].Junction.Commands);
        Assert.Contains(ability, result.Vitals.JunctionLearnedAbilities(row));
        foreach (int action in new[] { 21, 10, 22 })
        {
            Assert.Equal(2, result.Vitals.GetMagicCount(row, action));
        }
        Assert.NotEmpty(result.Changes);
        Assert.Contains(result.Changes, change => change.Owner == "character" && change.Row == row);
        JsonElement frozen = result.Changes.Single(change => change.Owner == "character" && change.Row == row).After;
        result.Vitals.Records[row].MagicInventory[0] = new(0, 0);
        Assert.True(JsonElement.DeepEquals(frozen, result.Changes.Single(change => change.Owner == "character" && change.Row == row).After));
    }

    [Fact]
    public void Installed_preparation_rejects_identity_mismatch_hp_overflow_and_unlearned_command_without_mutating_source()
    {
        NativeCharacterVitals original = InstalledNewGame();
        byte row = original.CombatParty.First(value => value != 255);
        byte character = original.Records[row].CharacterId;
        DiagnosticCharacterPreparation preparation = new(row, character, [], []);
        Assert.Throws<InvalidOperationException>(() => GameWorld.PrepareDiagnosticCharacters(original, new([], [preparation with { CharacterId = (byte)(character + 1) }], false)));
        Assert.Throws<InvalidOperationException>(() => GameWorld.PrepareDiagnosticCharacters(original, new([], [preparation with { HitPoints = ushort.MaxValue }], false)));
        int ability = Enumerable.Range(20, 19).Single(value => original.MenuDefinitions.CommandAbilityCommands[value - 20] == 2);
        Assert.Throws<InvalidOperationException>(() => GameWorld.PrepareDiagnosticCharacters(original, new([], [preparation with { MagicCommand = new(0, ability) }], false)));
        Assert.Throws<InvalidDataException>(() => GameWorld.PrepareDiagnosticCharacters(original, new([], [preparation with { Stocks = [new(63, 2)] }], false)));
    }

    private static NativeCharacterVitals InstalledNewGame()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for declarative preparation validation.");
        using GameAssets assets = new(directory);
        return assets.ReadNewGameCharacterVitals();
    }

    [Fact]
    public void Installed_generic_command_preparation_uses_existing_learned_kernel_ability_without_magic_gate()
    {
        NativeCharacterVitals original = InstalledNewGame();
        byte row = original.CombatParty.First(value => value != 255);
        byte character = original.Records[row].CharacterId;
        int ability = Enumerable.Range(20, 19).First(value => original.MenuDefinitions.CommandAbilityCommands[value - 20] != 2 && Enumerable.Range(0, 16).Any(id => original.GuardianForceAbilities(id).Any(entry => entry.Ability == value && entry.Learned)));
        int gf = Enumerable.Range(0, 16).First(id => original.GuardianForceAbilities(id).Any(entry => entry.Ability == ability && entry.Learned));
        DiagnosticBattlePreparation fixture = new([new(gf, true)], [new(row, character, [], [gf]) { Commands = [new(1, ability)] }], false);
        JsonElement before = JsonSerializer.SerializeToElement(original);
        DiagnosticPreparationResult result = GameWorld.PrepareDiagnosticCharacters(original, fixture);
        Assert.Equal(ability, (byte)(result.Vitals.Records[row].Junction.Commands >> 8));
        Assert.Contains(ability, result.Vitals.JunctionLearnedAbilities(row));
        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(original)));
        Assert.Throws<InvalidDataException>(() => (fixture with { Characters = [fixture.Characters[0] with { MagicCommand = new(1, ability) }] }).Validate());
    }

    [Fact]
    public void Installed_explicit_gf_learning_and_ap_use_native_progression_before_command_assignment()
    {
        NativeCharacterVitals original = InstalledNewGame();
        int gf = Enumerable.Range(0, 16).First(id => original.AcquireGuardianForce(id).GuardianForceAbilities(id).Any(entry => !entry.Learned && entry.RequiredPoints is > 0 and <= 255));
        NativeGuardianForceAbility available = original.AcquireGuardianForce(gf).GuardianForceAbilities(gf).First(entry => !entry.Learned && entry.RequiredPoints is > 0 and <= 255);
        DiagnosticBattlePreparation fixture = new([new(gf, true) { LearningAbility = available.Ability, AbilityPoints = checked((ushort)available.RequiredPoints) }], [], false);
        JsonElement before = JsonSerializer.SerializeToElement(original);
        DiagnosticPreparationResult result = GameWorld.PrepareDiagnosticCharacters(original, fixture);
        Assert.Contains(result.Vitals.GuardianForceAbilities(gf), entry => entry.Ability == available.Ability && entry.Learned);
        Assert.Contains(result.Changes, change => change.Owner == "guardian-force" && change.Row == gf);
        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(original)));
    }
}
