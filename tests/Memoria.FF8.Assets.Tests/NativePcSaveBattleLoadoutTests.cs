using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed partial class NativePcSaveConverterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_export_requires_separate_loadout_loss_consent_and_keeps_memoria_intent(bool emptyLimits)
    {
        GameState original = Defaults();
        GameState source = original with
        {
            BattleLoadouts = new()
            {
                Characters = ImmutableSortedDictionary<int, BattleCharacterLoadoutOverride>.Empty.Add(0, new() { WeaponId = 31, LimitFamilies = emptyLimits ? [] : [BattleLimitFamily.Duel, BattleLimitFamily.BlueMagic, BattleLimitFamily.Slot] })
            }
        };
        string before = JsonSerializer.Serialize(source);
        Assert.Contains("weapon and Limit Break", Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(source)).Message);
        Assert.Contains("weapon and Limit Break", Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(source, source.CharacterVitals!, allowGuardianForceLoss: true)).Message);
        byte[] exported = NativePcSaveConverter.Export(source, source.CharacterVitals!, allowBattleLoadoutLoss: true);
        GameState imported = NativePcSaveConverter.Import(exported, source, _ => "fixture", "en");
        Assert.Equal(original.CharacterVitals!.Records[0].Weapon, imported.CharacterVitals!.Records[0].Weapon);
        Assert.Null(imported.BattleLoadouts);
        Assert.Equal(before, JsonSerializer.Serialize(source));
    }
}
