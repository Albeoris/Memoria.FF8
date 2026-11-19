using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleLoadoutLabelsTests
{
    [Fact]
    public void Capability_labels_are_readable_and_use_the_localization_provider()
    {
        Assert.Equal("Machine gun", BattleLoadoutLabels.Capabilities(BattleWeaponCapabilities.MachineGun));
        Assert.Equal("Shot firearm", BattleLoadoutLabels.Capabilities(BattleWeaponCapabilities.ShotFirearm));
        Assert.Equal("Paired blades, Throwing spear", BattleLoadoutLabels.Capabilities(BattleWeaponCapabilities.PairedBlades | BattleWeaponCapabilities.ThrowingSpear));
        Assert.Equal("Unarmed fighting", BattleLoadoutLabels.Style(1));
        Assert.Equal("translated", BattleLoadoutLabels.Capabilities(BattleWeaponCapabilities.MachineGun, (_, _) => "translated"));
    }

    [Fact]
    public void Canonical_family_facts_show_learning_story_suppression_and_ammo_without_mutation()
    {
        NativeItemStock[] items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray();
        items[0] = new(17, 4);
        items[1] = new(18, 9);
        GameState state = new()
        {
            LimitProgress = new(new string('0', 64), 5, 3, 0x11, 0, [1, 0, 0, 0, 0, 0, 0, 0]),
            SavedBattleFlags = new(NativeInputConfiguration.OriginalInitHash, 0x10),
            Economy = new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = new string('0', 64), Items = items, Gil = 0, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 },
            ShotAvailability = new(1, ImmutableArray.Create<byte>(17, 18, 19, 20, 21, 22, 23, 24), new string('0', 64), new string('0', 64))
        };
        string before = JsonSerializer.Serialize(state);
        Assert.Contains(BattleLoadoutLabels.CanonicalFacts(state, BattleLimitFamily.BlueMagic), line => line.Contains("2/16", StringComparison.Ordinal));
        Assert.Contains(BattleLoadoutLabels.CanonicalFacts(state, BattleLimitFamily.Combine), line => line.Contains("suppressed", StringComparison.Ordinal));
        Assert.Contains(BattleLoadoutLabels.CanonicalFacts(state, BattleLimitFamily.AngelWing), line => line.Contains("locked", StringComparison.Ordinal));
        Assert.Contains(BattleLoadoutLabels.CanonicalFacts(state, BattleLimitFamily.Shot), line => line.Contains("4 rounds in 1", StringComparison.Ordinal));
        Assert.Equal(before, JsonSerializer.Serialize(state));
        GameState unlocked = state with { SavedBattleFlags = state.SavedBattleFlags! with { Value = 0x20 } };
        Assert.Contains(BattleLoadoutLabels.CanonicalFacts(unlocked, BattleLimitFamily.AngelWing), line => line.Contains("unlocked", StringComparison.Ordinal));
        Assert.Contains(BattleLoadoutLabels.CanonicalFacts(unlocked, BattleLimitFamily.Combine), line => line.Contains("eligible assigned", StringComparison.Ordinal));
    }
}
