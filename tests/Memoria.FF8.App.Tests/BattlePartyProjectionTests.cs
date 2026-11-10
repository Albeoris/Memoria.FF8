using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

// Installed canonical party records exercise Memoria's projection; native combat-stat parity is not claimed.
public sealed class BattlePartyProjectionTests
{
    [Fact]
    public void Every_installed_command_ability_reaches_its_battle_menu_route()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets resources = new(game.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(game);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals() with { CombatParty = [0, 255, 255] };
        GameState state = new() { CharacterVitals = vitals, CharacterAppearance = game.ReadNewGameCharacterAppearance() };
        BattleNativeAction[] nativeActions = resources.NativeActions.ToArray();
        NativeCharacterVital initial = vitals.Records[0];
        for (int index = 0; index < vitals.MenuDefinitions.CommandAbilityCommands.Length; index++)
        {
            byte command = vitals.MenuDefinitions.CommandAbilityCommands[index];
            int reservedAbility = command == 38 ? 20 : 38;
            byte reservedCommand = vitals.MenuDefinitions.CommandAbilityCommands[reservedAbility - 20];
            uint commands = (uint)(20 + index) << (8 * (index % 3)) | (uint)reservedAbility << 24;
            vitals.Records[0] = initial with { Junction = initial.Junction with { Commands = commands } };
            BattleCombatantDefinition actor = Assert.Single(BattlePartyProjection.Create(state, null, catalog, resources));
            Assert.Equal(command, actor.NativeCommandSlots[index % 3 + 1]);
            Assert.DoesNotContain(reservedCommand, actor.NativeCommandSlots);
            Assert.DoesNotContain(actor.NativeCommands, binding => binding.Command == reservedCommand);
            if (command == 0)
            {
                Assert.Equal(BattleAbility.Attack, Assert.Single(actor.Abilities));
                Assert.Equal((byte)1, Assert.Single(actor.NativeCommands).Command);
                continue;
            }
            BattleNativeCommandBinding binding = resources.ReadNativeCommand(command);
            Assert.Contains(binding, actor.NativeCommands);
            BattleAbility ability = command switch
            {
                2 => BattleAbility.Magic,
                3 => BattleAbility.GuardianForce,
                4 => BattleAbility.Item,
                6 => BattleAbility.Draw,
                _ => BattleAbility.NativeCommand
            };
            Assert.Contains(ability, actor.Abilities);
            BattleSession battle = new(new BattleDefinition
            {
                Combatants = [actor with { InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Enemy" }],
                NativeActions = nativeActions
            }, BattleAtbMode.Normal);
            IReadOnlyList<BattleActionDefinition> choices = battle.GetNativePlayerActionChoices(actor.Id, false);
            if (ability != BattleAbility.NativeCommand)
            {
                Assert.Empty(choices);
                continue;
            }
            BattleActionDefinition choice = Assert.Single(choices);
            Assert.Equal(binding.Name, choice.Name);
            Assert.Equal(command, choice.NativeAction!.Command);
            battle.QueueAction(actor.Id, choice.Allies ? actor.Id : "enemy", choice.Ability, choice.Id);
            Assert.Equal(command, Assert.Single(battle.QueuedActions).NativeQueuedCommand);
        }
    }

    [Theory]
    [InlineData(20, 2, BattleAbility.Magic, 1)]
    [InlineData(30, 28, BattleAbility.NativeCommand, 28 << 16)]
    public void Legacy_fourth_command_is_removed_from_menu_and_retains_queued_and_active_actions(int savedAbility, byte command, BattleAbility ability, int actionId)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets resources = new(game.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(game);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals() with { CombatParty = [0, 255, 255] };
        NativeCharacterVital initial = vitals.Records[0];
        NativeMagicStock[] inventory = initial.MagicInventory.ToArray();
        inventory[0] = new(1, 4);
        uint savedCommands = (uint)savedAbility << 24;
        vitals.Records[0] = initial with { Junction = initial.Junction with { Commands = savedCommands }, MagicInventory = inventory };
        GameState state = new() { CharacterVitals = vitals, CharacterAppearance = game.ReadNewGameCharacterAppearance() };
        BattleCombatantDefinition current = Assert.Single(BattlePartyProjection.Create(state, null, catalog, resources)) with { InitialAtb = BattleSession.AtbMaximum };
        BattleNativeCommandBinding obsolete = resources.ReadNativeCommand(command);
        BattleCombatantDefinition legacy = current with { Abilities = [.. current.Abilities, ability], NativeCommands = current.NativeCommands.Add(obsolete) };
        BattleSession old = new(new BattleDefinition
        {
            Combatants = [legacy, new() { Id = "enemy", Name = "Enemy" }],
            MagicVitals = vitals,
            ActionDefinitions = resources.ActionDefinitions.ToArray(),
            NativeActions = resources.NativeActions.ToArray(),
            NativeCommandNames = resources.ReadNativeCommandNames(),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory()
        }, BattleAtbMode.Normal);
        old.QueueAction(current.Id, "enemy", ability, actionId);
        BattleSnapshot queued = old.Capture();
        BattleSession restored = NormalizeAndRestore(queued);
        Assert.Equal(JsonSerializer.Serialize(Assert.Single(old.QueuedActions)), JsonSerializer.Serialize(Assert.Single(restored.QueuedActions)));

        old.Tick();
        restored.Tick();
        Assert.NotNull(old.ActiveAction);
        BattleSession restoredActive = NormalizeAndRestore(old.Capture());
        Assert.Equal(JsonSerializer.Serialize(old.ActiveAction), JsonSerializer.Serialize(restoredActive.ActiveAction));
        Assert.Equal(JsonSerializer.Serialize(restored.Capture()), JsonSerializer.Serialize(restoredActive.Capture()));

        BattleCombatantDefinition unrelated = legacy with { NativeCommands = current.NativeCommands.Add(obsolete with { Name = "Unexpected command" }) };
        BattleSnapshot inconsistent = queued with { Definition = queued.Definition with { Combatants = [unrelated, queued.Definition.Combatants[1]] } };
        Assert.Same(inconsistent, BattlePartyProjection.RestoreLegacyCommandProjection(inconsistent, resources));

        BattleSession NormalizeAndRestore(BattleSnapshot snapshot)
        {
            BattleSnapshot corrected = BattlePartyProjection.RestoreLegacyCommandProjection(snapshot, resources);
            BattleDefinition expected = snapshot.Definition with { Combatants = [current, snapshot.Definition.Combatants[1]] };
            GameWorld.ValidateBattleDefinition(expected, corrected.Definition);
            Assert.Equal(savedCommands, corrected.Definition.MagicVitals!.Records[0].Junction.Commands);
            Assert.Equal(savedCommands, corrected.MagicVitals!.Records[0].Junction.Commands);
            Assert.Same(snapshot.QueuedActions, corrected.QueuedActions);
            Assert.Same(snapshot.ActiveAction, corrected.ActiveAction);
            Assert.Same(corrected, BattlePartyProjection.RestoreLegacyCommandProjection(corrected, resources));
            BattleSession session = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(corrected))!);
            Assert.DoesNotContain(ability, session.Combatants.Single(actor => actor.IsPlayer).Abilities);
            Assert.Empty(session.GetNativePlayerActionChoices(current.Id, false));
            return session;
        }
    }

    [Fact]
    public void Laboratory_uses_current_party_order_names_hp_costume_weapon_and_gf_owners()
    {
        string directory = TestResources.GameDirectory;
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
        string directory = TestResources.GameDirectory;
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
    public void Saved_status_bit7_has_a_battle_owner_and_survives_restore_and_factual_result_capture()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        using BattleAssets resources = new(directory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        vitals.Records[0] = vitals.Records[0] with { Status = 0x80 };
        vitals = vitals with { CombatParty = [0, 255, 255] };
        NativeEconomy economy = assets.ReadNewGameEconomy();
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 999, CharacterVitals = vitals, CharacterAppearance = assets.ReadNewGameCharacterAppearance(), Economy = economy });
        BattleCombatantDefinition party = Assert.Single(BattlePartyProjection.Create(session.State, null, catalog, resources));

        Assert.Equal((ushort)0x80, party.NativeRetainedStatusBits);

        BattleDefinition definition = new() { EncounterId = 512, Combatants = [party, new() { Id = "enemy", Name = "Component enemy", ModelId = 28 }], Items = economy.Items.Where(stock => stock.ItemId > 0 && stock.Count > 0).Select(stock => new BattleStock(stock.ItemId, stock.Count)).ToArray() };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        battle.InstantVictory();
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Assert.Equal((ushort)0x80, restored.Combatants.Single(actor => actor.IsPlayer).NativeRetainedStatusBits);
        using GameWorld world = new(session, directory);
        BattleEncounterOccurrence occurrence = new("retained-status-component", 512, resources.FormationSourceHash(512), SceneKind.Field, 999, 0, new string('A', 64), null);

        BattlePartyResult result = Assert.Single(world.CaptureBattleOutcome(restored, occurrence).Party);

        Assert.Equal((ushort)0x80, result.StatusBefore);
        Assert.Equal((ushort)0x80, result.StatusAfter);
        Assert.Equal((ushort)0x80, session.State.CharacterVitals!.Records[0].Status);
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
