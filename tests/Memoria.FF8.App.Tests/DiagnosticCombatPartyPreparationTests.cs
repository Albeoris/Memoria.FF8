using Memoria.FF8.Assets;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticCombatPartyPreparationTests
{
    [Theory]
    [InlineData(new byte[] { 3, 3, 255 })]
    [InlineData(new byte[] { 3, 255 })]
    [InlineData(new byte[] { 8, 255, 255 })]
    [InlineData(new byte[] { 255, 255, 255 })]
    public void Explicit_party_rejects_invalid_native_saved_row_selections(byte[] rows)
    {
        DiagnosticBattlePreparation fixture = new([], [], false) { CombatParty = rows.ToImmutableArray() };
        Assert.Throws<InvalidDataException>(fixture.Validate);
    }

    [Fact]
    public void Installed_party_preparation_selects_the_original_owner_without_replacing_saved_records()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for original character preparation validation.");
        using GameAssets assets = new(directory);
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals();
        int row = Array.FindIndex(original.Records, record => record.CharacterId == 3);
        Assert.InRange(row, 0, 7);
        JsonElement before = JsonSerializer.SerializeToElement(original);
        DiagnosticBattlePreparation fixture = new([], [new(row, 3, [], []) { HitPoints = 1 }], false) { CombatParty = [(byte)row, 255, 255] };
        DiagnosticPreparationResult result = GameWorld.PrepareDiagnosticCharacters(original, fixture);
        Assert.Equal(new byte[] { (byte)row, 255, 255 }, result.Vitals.CombatParty);
        Assert.Equal((byte)3, result.Vitals.Records[row].CharacterId);
        Assert.Equal(original.Records[row].Experience, result.Vitals.Records[row].Experience);
        Assert.Equal(original.Records[row].Weapon, result.Vitals.Records[row].Weapon);
        Assert.Equal(original.Records[row].Junction, result.Vitals.Records[row].Junction);
        Assert.Equal((ushort)1, result.Vitals.Records[row].HitPoints.Minimum);
        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(original)));
        Assert.Contains(result.Changes, change => change.Owner == "combat-party");
        Assert.Throws<InvalidOperationException>(() => GameWorld.PrepareDiagnosticCharacters(original, fixture with { Characters = [new(row, 0, [], [])] }));
    }
}
