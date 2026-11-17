using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEnemyCasterFixtureTests
{
    [Fact]
    public void Preserved_formation_keeps_distinct_authored_placements_in_slot_order()
    {
        BattleEnemyPlacement[] enemies = Enumerable.Range(0, 4).Select(slot => new BattleEnemyPlacement(slot, 40 + slot, new Vector3(slot * 100, 0, -2000), 20, false, false)).ToArray();
        BattleEncounter formation = new(509, 142, 0, 0, 0, enemies.Reverse().ToArray());
        Assert.Throws<InvalidDataException>(() => NativeBattleComponentInputs.SelectEnemy(formation));
        Assert.Equal(enemies, NativeBattleComponentInputs.SelectEnemies(formation, true));
    }

    [Fact]
    public void Installed_formation509_Doom_caster_has_native_model138_and_sequence11()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check the actual Doom caster row.");
        using BattleAssets assets = new(directory!);
        BattleEnemyPlacement caster = Assert.Single(assets.LoadEncounter(509).Enemies, enemy => enemy.Slot == 1 && enemy.Enabled);
        Assert.Equal(122, caster.ModelId);
        Assert.Equal(138, caster.NativeModelId);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 263 && row.MagicId == 8);
        Assert.Equal((byte)11, NativeBattleComponentInputs.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
    }

    [Theory]
    [InlineData(" ", "Original enemy model 123")]
    [InlineData("", "Original enemy model 123")]
    [InlineData("Original name", "Original name")]
    public void Component_label_preserves_named_resources_and_identifies_anonymous_host_rows(string original, string expected)
    {
        Assert.Equal(expected, NativeBattleComponentInputs.ComponentEnemyName(original, 123));
    }

    [Fact]
    public void Original_formation509_hidden_model_has_a_space_name_without_becoming_a_fabricated_native_name()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check the anonymous formation row.");
        using BattleAssets assets = new(directory!);
        BattleEnemyPlacement enemy = Assert.Single(NativeBattleComponentInputs.SelectEnemies(assets.LoadEncounter(509), true), enemy => enemy.Slot == 0);
        Assert.Equal(123, enemy.ModelId);
        Assert.Equal(251, enemy.Level);
        Assert.True(enemy.Hidden);
        string original = assets.ReadEnemyName(enemy.ModelId);
        Assert.Equal(" ", original);
        Assert.Equal("Original enemy model 123", NativeBattleComponentInputs.ComponentEnemyName(original, enemy.ModelId));
        Assert.Equal(" ", assets.ReadEnemyName(enemy.ModelId));
    }

}
