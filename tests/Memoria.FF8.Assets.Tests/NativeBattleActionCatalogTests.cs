using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleActionCatalogTests
{
    [Fact]
    public void Installed_catalog_keeps_every_player_spell_item_and_junctionable_GF_including_special_effects()
    {
        using BattleAssets assets = InstalledAssets();

        Assert.Equal(Enumerable.Range(1, 56), assets.ActionDefinitions.Where(action => action.Ability == BattleAbility.Magic).Select(action => action.Id));
        Assert.Equal(Enumerable.Range(1, 32), assets.ActionDefinitions.Where(action => action.Ability == BattleAbility.Item).Select(action => action.Id));
        Assert.Equal(Enumerable.Range(0, 16), assets.ActionDefinitions.Where(action => action.Ability == BattleAbility.GuardianForce).Select(action => action.Id));
        Assert.Equal(Enumerable.Range(0, 384), assets.ActionDefinitions.Where(action => action.Ability == BattleAbility.EnemyAbility).Select(action => action.Id));
        Assert.Equal(8, assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Name == "Demi").NativeAction!.AttackType);
        Assert.Equal((uint)0x80, assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Name == "Reflect").NativeAction!.Statuses1);
        Assert.Equal((uint)0x100, assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Name == "Aura").NativeAction!.Statuses1);
        Assert.Equal((uint)0x1000000, assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Name == "Meltdown").NativeAction!.Statuses1);
        Assert.Equal((ushort)4, assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Name == "Break").NativeAction!.Statuses0);
        Assert.All(assets.ActionDefinitions, action => Assert.Equal(action.NativeAction!.AttackFlags, action.NativeAttackFlags));
    }

    [Fact]
    public void Shared_kernel_consumers_preserve_their_actual_native_command_and_source_records()
    {
        using BattleAssets assets = InstalledAssets();

        foreach ((byte command, byte canonical) in new (byte, byte)[] { (6, 2), (16, 2), (247, 2), (13, 4), (236, 8), (14, 237), (238, 237), (239, 241), (244, 240), (245, 240) })
        {
            BattleNativeAction[] originals = assets.NativeActions.Where(action => action.Command == canonical).ToArray();
            BattleNativeAction[] variants = assets.NativeActions.Where(action => action.Command == command).ToArray();
            Assert.Equal(originals.Length, variants.Length);
            foreach (BattleNativeAction variant in variants)
            {
                BattleNativeAction original = originals.Single(action => action.ActionId == variant.ActionId);
                Assert.Equal(original with { Command = command }, variant);
            }
        }
    }

    [Fact]
    public void GF_targeting_comes_from_the_original_target_byte_and_retains_special_damage_types()
    {
        using BattleAssets assets = InstalledAssets();
        BattleActionDefinition[] guardians = assets.ActionDefinitions.Where(action => action.Ability == BattleAbility.GuardianForce).ToArray();

        Assert.Equal(new[] { 6, 9 }, guardians.Where(action => action.Allies).Select(action => action.Id));
        Assert.False(guardians[14].AllTargets);
        Assert.Equal((byte)0x42, guardians[14].NativeAction!.TargetFlags);
        Assert.Equal(20, guardians[5].NativeAction!.AttackType);
        Assert.Equal(29, guardians[13].NativeAction!.AttackType);
        Assert.Equal(15, guardians[15].NativeAction!.AttackType);
        Assert.Equal((byte)250, guardians[15].NativeAction!.PowerModifier);
        Assert.All(guardians, action => Assert.Equal(64 + action.Id, action.NativeAction!.ActionId));
    }

    [Fact]
    public void Raw_catalog_retains_original_limit_families_and_every_Blue_Magic_crisis_row()
    {
        using BattleAssets assets = InstalledAssets();

        Assert.Equal(4, assets.NativeKernelRecords.Count(record => record.Section == 5));
        Assert.Equal(16, assets.NativeKernelRecords.Count(record => record.Section == 9));
        Assert.Equal(12, assets.NativeKernelRecords.Count(record => record.Section == 10));
        Assert.Equal(5, assets.NativeKernelRecords.Count(record => record.Section == 18));
        Assert.Equal(16, assets.NativeKernelRecords.Count(record => record.Section == 19));
        Assert.Equal(64, assets.NativeKernelRecords.Count(record => record.Section == 20));
        Assert.Equal(8, assets.NativeKernelRecords.Count(record => record.Section == 21));
        Assert.Equal(10, assets.NativeKernelRecords.Count(record => record.Section == 22));
        Assert.Equal(5, assets.NativeKernelRecords.Count(record => record.Section == 25));
        BattleNativeAction[] blueMagic = assets.NativeActions.Where(action => action.Command == 15).ToArray();
        Assert.Equal(64, blueMagic.Length);
        foreach (IGrouping<ushort, BattleNativeAction> spell in blueMagic.GroupBy(action => action.ActionId))
        {
            Assert.Equal(new byte?[] { 1, 2, 3, 4 }, spell.Select(action => action.CrisisLevel));
            Assert.All(spell, action =>
            {
                Assert.Equal(3, action.CharacterId);
                Assert.Equal(action.ActionId * 4 + action.CrisisLevel - 1, action.SupplementalKernelIndex);
                Assert.Equal(16, action.RawRecord.Length);
                Assert.Equal(8, action.SupplementalRecord.Length);
            });
        }
    }

    [Fact]
    public void Original_action_metadata_survives_an_independent_battle_snapshot_without_rebuilding_from_a_filtered_catalog()
    {
        using BattleAssets assets = InstalledAssets();
        BattleSession battle = new(new BattleDefinition { ActionDefinitions = assets.ActionDefinitions.ToArray(), Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        string original = JsonSerializer.Serialize(battle.Capture());

        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(original)!);

        Assert.Equal(original, JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(16, restored.ActionDefinitions.Count(action => action.Ability == BattleAbility.GuardianForce));
        Assert.Equal((uint)0x1000000, restored.ActionDefinitions.Single(action => action.Name == "Meltdown").NativeAction!.Statuses1);
    }

    private static BattleAssets InstalledAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        return new BattleAssets(directory);
    }
}
