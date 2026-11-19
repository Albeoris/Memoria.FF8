using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InstalledBattlePracticeLoadoutTests
{
    [Theory]
    [InlineData(0, 8)]
    [InlineData(4, 1)]
    [InlineData(10, 9)]
    public void Synthetic_laboratory_actor_receives_real_native_metadata_without_rewriting_native_equipment(int recipient, int donor)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeCharacterVitals defaults = game.ReadNewGameCharacterVitals().WithGuestBattleRecords();
        string before = JsonSerializer.Serialize(defaults);
        int weapon = assets.WeaponDefinitions.First(entry => entry.CharacterId == donor).Id;
        BattleCombatantDefinition scenario = new() { Id = "recipient", Name = "Recipient", IsPlayer = true, ModelId = recipient, WeaponId = weapon, MaximumHp = 800, CurrentHp = 700, NativeLimitFamilies = [] };
        BattleCombatantDefinition prepared = InstalledBattlePractice.PreparePlayer(scenario, defaults, assets);
        Assert.Equal(recipient, prepared.ModelId);
        Assert.Equal(recipient, prepared.NativePlayerStatRules!.CharacterId);
        Assert.Equal(recipient, prepared.NativeAttack!.CharacterId);
        Assert.Equal(donor, prepared.WeaponProfile!.ResourceOwner);
        Assert.Equal(weapon, prepared.WeaponId);
        Assert.Equal(assets.NativeKernelRecords.Single(record => record.Section == 4 && record.Index == weapon).Bytes, prepared.NativePlayerStatRules.WeaponRecord);
        BattleNativePlayerStatistics statistics = prepared.NativePlayerStatRules.Project(prepared.NativeMagicInventory!, prepared.NativeGrowth!);
        Assert.Equal(statistics.MaximumHp, prepared.MaximumHp);
        Assert.Equal(Math.Min(700, (int)statistics.MaximumHp), prepared.CurrentHp);
        Assert.Empty(prepared.NativeLimitFamilies!);
        Assert.NotNull(prepared.NativeGrowth);
        Assert.Equal(32, prepared.NativeMagicInventory!.Length);
        Assert.Equal(before, JsonSerializer.Serialize(defaults));
        Assert.Null(scenario.NativeAttack);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Native_laboratory_HP_preserves_valid_injury_and_KO_and_clamps_above_the_projected_maximum(int currentHp)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeCharacterVitals defaults = game.ReadNewGameCharacterVitals();
        BattleCombatantDefinition source = new() { Id = "party-0", Name = "Squall", IsPlayer = true, ModelId = 0, MaximumHp = 1000, CurrentHp = currentHp };
        BattleCombatantDefinition prepared = InstalledBattlePractice.PreparePlayer(source, defaults, assets);
        BattleNativePlayerStatistics statistics = prepared.NativePlayerStatRules!.Project(prepared.NativeMagicInventory!, prepared.NativeGrowth!);
        Assert.Equal(statistics.MaximumHp, prepared.MaximumHp);
        Assert.Equal(Math.Min(currentHp, statistics.MaximumHp), prepared.CurrentHp);
        Assert.Equal(currentHp, source.CurrentHp);
        BattleCombatantDefinition again = InstalledBattlePractice.PreparePlayer(prepared, defaults, assets);
        Assert.Equal(prepared.MaximumHp, again.MaximumHp);
        Assert.Equal(prepared.CurrentHp, again.CurrentHp);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Foreign_machine_gun_laboratory_scenario_enters_and_completes_a_native_attack(int recipient)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        int weapon = assets.WeaponDefinitions.First(entry => entry.CharacterId == 8).Id;
        BattleDefinition source = new()
        {
            StageId = assets.StageIds.First(),
            Combatants =
            [
                new() { Id = "party-0", Name = "Recipient", IsPlayer = true, ModelId = recipient, WeaponId = weapon, MaximumHp = 1000, CurrentHp = 1000, NativeLimitFamilies = [] },
                new() { Id = "enemy-0", Name = "Training target", ModelId = 1, MaximumHp = 1000, CurrentHp = 1000 }
            ]
        };
        BattleSession battle = InstalledBattlePractice.Create(source, BattleAtbMode.TurnBased, game, assets);
        battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true, readinessActorId: "party-0");
        TickUntil(() => battle.NativeEntryReady, "laboratory native entry");
        BattleCombatant actor = battle.Combatants.Single(entry => entry.IsPlayer);
        actor.Atb = BattleSession.AtbMaximum;
        TickUntil(() => battle.CanChoosePlayerCommand(actor.Id), "laboratory readiness");
        battle.QueueAttack(actor.Id, "enemy-0");
        TickUntil(() => battle.Events.Any(entry => entry.Kind == "ActionComplete" && entry.ActorId == actor.Id) && battle.ActiveAction is null && battle.Presentation?.HasActiveAction != true, "laboratory foreign attack");
        Assert.Contains(battle.Events, entry => entry.Kind == "Damage" && entry.ActorId == actor.Id && entry.Amount > 0);
        Assert.Equal(weapon, battle.GetCombatantDefinition(actor.Id).WeaponId);
        Assert.Equal(recipient, actor.ModelId);
        Assert.Null(source.Combatants[0].NativeAttack);
        Assert.Empty(battle.GetCombatantDefinition(actor.Id).NativeLimitFamilies!);

        void TickUntil(Func<bool> complete, string boundary)
        {
            for (int callback = 0; callback < 1800 && !complete(); callback++)
            {
                battle.Tick();
            }
            Assert.True(complete(), $"Recipient {recipient}, machine gun {weapon}: {boundary}");
        }
    }
}
