using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

// Installed canonical party records exercise Memoria's projection; native combat-stat parity is not claimed.
public sealed class BattlePartyProjectionTests
{
    [Fact]
    public void Laboratory_uses_current_party_order_names_hp_costume_weapon_and_gf_owners()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        using GameAssets assets = new(directory);
        BattleAssets resources = new(directory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeCharacterAppearance appearance = assets.ReadNewGameCharacterAppearance();
        vitals.Records[0] = vitals.Records[0] with { HitPoints = new NativeHpWord(123, 123), Junction = vitals.Records[0].Junction with { GuardianForces = 3 } };
        vitals = vitals with { CombatParty = [4, 1, 0] };
        Dictionary<int, string> names = new() { [0] = "Custom Squall", [1] = "Custom Rinoa" };

        BattleCombatantDefinition[] party = BattlePartyProjection.Create(new GameState { CharacterVitals = vitals, CharacterAppearance = appearance }, names, catalog, resources);

        Assert.Equal(new[] { 4, 1, 0 }, party.Select(actor => actor.ModelId));
        Assert.Equal(new[] { "Custom Rinoa", catalog.CharacterName(1), "Custom Squall" }, party.Select(actor => actor.Name));
        Assert.Equal(123, party[2].CurrentHp);
        Assert.Equal(vitals.Definitions.CharacterMaximum(vitals.Records[0]), party[2].MaximumHp);
        Assert.Equal(appearance.Records[0].Dress, party[2].Costume);
        Assert.Equal((int)vitals.Records[0].Weapon, party[2].WeaponId);
        Assert.Equal(resources.WeaponDefinitions[vitals.Records[0].Weapon].Name, party[2].WeaponName);
        Assert.Equal(new[] { 0, 1 }, party[2].EquipmentIds);
        Assert.Equal(new byte[] { 4, 1, 0 }, vitals.CombatParty);
    }

    [Fact]
    public void Title_laboratory_uses_the_authentic_new_game_party_without_changing_a_session()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        using GameAssets assets = new(directory);
        BattleAssets resources = new(directory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        GameSession title = new(new GameState());
        GameState before = title.State;

        BattleCombatantDefinition[] party = BattlePartyProjection.CreateNewGame(assets, catalog, resources);

        Assert.Equal(initial.CombatParty.Where(row => row != 255).Select(row => (int)initial.Records[row].CharacterId), party.Select(actor => actor.ModelId));
        Assert.Equal(initial.CombatParty.Where(row => row != 255).Select(row => (int)initial.Records[row].HitPoints.Minimum), party.Select(actor => actor.CurrentHp));
        Assert.Equal("Squall", Assert.Single(party).Name);
        Assert.Same(before, title.State);
        Assert.Null(title.State.CharacterVitals);
    }

    [Fact]
    public void Laboratory_refreshes_party_after_a_previous_scenario_and_uses_live_battle_hp()
    {
        BattleCombatantDefinition current = new() { Id = "party-4", Name = "Rinoa", ModelId = 4, IsPlayer = true, MaximumHp = 300, CurrentHp = 200, WeaponId = 10, EquipmentIds = [2] };
        BattleDefinition scenario = new() { StageId = 7, Combatants = [new() { Id = "party-0", Name = "Old scenario member", IsPlayer = true }, new() { Id = "enemy-0", Name = "Enemy" }] };
        BattleSession battle = new(new BattleDefinition { Combatants = [current with { CurrentHp = 111 }, scenario.Combatants[1]] }, BattleAtbMode.Normal);

        BattleDefinition refreshed = BattleDebugController.InitializeParty(scenario, [current], battle);

        BattleCombatantDefinition player = Assert.Single(refreshed.Combatants, actor => actor.IsPlayer);
        Assert.Equal("Rinoa", player.Name);
        Assert.Equal(4, player.ModelId);
        Assert.Equal(111, player.CurrentHp);
        Assert.Equal(10, player.WeaponId);
        Assert.Equal(7, refreshed.StageId);
        Assert.Equal("Enemy", Assert.Single(refreshed.Combatants, actor => !actor.IsPlayer).Name);
        player.EquipmentIds[0] = 9;
        Assert.Equal(2, current.EquipmentIds[0]);
        Assert.Equal(200, current.CurrentHp);
        Assert.Equal("Old scenario member", scenario.Combatants[0].Name);
    }
}
