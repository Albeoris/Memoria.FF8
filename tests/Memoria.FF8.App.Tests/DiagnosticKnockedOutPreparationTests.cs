using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticKnockedOutPreparationTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(null, true)]
    public void KoRequiresBothExplicitDeclarationAndZeroSavedHp(int? hp, bool knockedOut)
    {
        DiagnosticBattlePreparation recipe = new([], [new(1, 1, [], []) { HitPoints = hp is int value ? checked((ushort)value) : null, KnockedOut = knockedOut }], true);
        Assert.Throws<InvalidDataException>(recipe.Validate);
    }

    [Fact]
    public void InstalledKoPreparationPublishesBothSavedOwnersAndPreservesTheLivingCaster()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for original KO preparation.");
        using GameAssets assets = new(directory);
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals();
        int row = Enumerable.Range(1, 7).First(row => original.Records[row].CharacterId < 8 && original.Records[row].CharacterId != original.Records[0].CharacterId);
        JsonElement before = JsonSerializer.SerializeToElement(original);
        DiagnosticBattlePreparation fixture = new([], [new(row, original.Records[row].CharacterId, [], []) { HitPoints = 0, KnockedOut = true }], true) { CombatParty = [0, (byte)row, 255] };

        DiagnosticPreparationResult result = GameWorld.PrepareDiagnosticCharacters(original, fixture);

        Assert.Equal(new NativeHpWord(0, 0), result.Vitals.Records[row].HitPoints);
        Assert.Equal((ushort)(original.Records[row].Status | 1), result.Vitals.Records[row].Status);
        Assert.Equal(original.Records[0], result.Vitals.Records[0]);
        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(original)));
        Assert.Contains(result.Changes, change => change.Owner == "character" && change.Row == row);
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(result.Vitals))!;
        restored.Validate();
        Assert.Equal(result.Vitals.Records[row], restored.Records[row]);
    }
}
