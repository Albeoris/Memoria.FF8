using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEnemyCasterFixtureTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Preserved_enemy_caster_selects_the_actual_scene_slot_instead_of_the_first_enemy(byte caster)
    {
        BattleEnemyPlacement[] enemies = Enumerable.Range(0, 4).Select(slot => new BattleEnemyPlacement(slot, 40 + slot, new Vector3(slot * 100, 0, -2000), 20, false, false)).ToArray();
        BattleEncounter formation = new(509, 142, 0, 0, 0, enemies.Reverse().ToArray());
        NativeEffectFixtureRequest request = Request(caster, true);
        NativeBattleEffectFixtureExporter.Validate(request);
        Assert.Same(enemies[caster - 3], NativeBattleEffectFixtureExporter.SelectCasterEnemy(formation, request));
        Assert.Equal(enemies.OrderBy(enemy => enemy.Slot), NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
    }

    [Fact]
    public void Unsupported_or_absent_rows_fail_without_fabricating_a_placement()
    {
        BattleEnemyPlacement first = new(0, 68, Vector3.Zero, 20, false, false);
        BattleEncounter formation = new(509, 142, 0, 0, 0, [first]);
        foreach (byte caster in new byte[] { 1, 2, 7 })
        {
            Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.Validate(Request(caster, true)));
        }
        foreach (byte caster in new byte[] { 4, 5, 6 })
        {
            Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.Validate(Request(caster, false)));
            Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.SelectCasterEnemy(formation, Request(caster, true)));
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.SelectCasterEnemy(formation with { Enemies = [first, first with { Slot = 1, Enabled = false }] }, Request(4, true)));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.Validate(Request(4, true) with { OrdinaryMagicAdmission = true }));
        Assert.Same(first, NativeBattleEffectFixtureExporter.SelectCasterEnemy(formation, Request(3, false)));
    }

    [Fact]
    public void Original_Doom_caster_is_physical4_model122_with_its_own_AI_sequence11()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check the actual Doom caster row.");
        using BattleAssets assets = new(directory!);
        BattleEncounter formation = assets.LoadEncounter(509);
        BattleEnemyPlacement caster = NativeBattleEffectFixtureExporter.SelectCasterEnemy(formation, Request(4, true));
        Assert.Equal(1, caster.Slot);
        Assert.Equal(122, caster.ModelId);
        Assert.Equal(138, caster.NativeModelId);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 263 && row.MagicId == 8);
        Assert.Equal((byte)11, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
    }

    [Theory]
    [InlineData(" ", "Original enemy model 123")]
    [InlineData("", "Original enemy model 123")]
    [InlineData("Original name", "Original name")]
    public void Component_label_preserves_named_resources_and_identifies_anonymous_host_rows(string original, string expected)
    {
        Assert.Equal(expected, NativeBattleEffectFixtureExporter.ComponentEnemyName(original, 123));
    }

    [Fact]
    public void Original_formation509_hidden_model_has_a_space_name_without_becoming_a_fabricated_native_name()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check the anonymous formation row.");
        using BattleAssets assets = new(directory!);
        BattleEnemyPlacement enemy = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(assets.LoadEncounter(509), true), enemy => enemy.Slot == 0);
        Assert.Equal(123, enemy.ModelId);
        Assert.Equal(251, enemy.Level);
        Assert.True(enemy.Hidden);
        string original = assets.ReadEnemyName(enemy.ModelId);
        Assert.Equal(" ", original);
        Assert.Equal("Original enemy model 123", NativeBattleEffectFixtureExporter.ComponentEnemyName(original, enemy.ModelId));
        Assert.Equal(" ", assets.ReadEnemyName(enemy.ModelId));
    }

    private static NativeEffectFixtureRequest Request(byte caster, bool preserve)
    {
        return new("enemy-caster", 8, 8, 263, 0, 800, 240) { EncounterId = 509, CasterActorIndex = caster, PreserveOriginalFormationEnemies = preserve };
    }
}
