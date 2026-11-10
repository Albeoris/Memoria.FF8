using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ChocoboWorldInstalledItemTests
{
    [Theory]
    [InlineData("MEMORIA_FF8_GAME_DIR")]
    [InlineData("MEMORIA_FF8_REMASTERED_DIR")]
    public void Memoria_amulet_teaches_a_usable_MiniMog_command_on_a_cold_PocketStation(string installationVariable)
    {
        using GameAssets game = ReadInstalled(installationVariable);
        using BattleAssets resources = new(game.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(game);
        GameState source = new()
        {
            GameplayPreferences = new() { ChocoboWorldMode = ChocoboWorldMode.Memoria },
            CharacterVitals = game.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0),
            CharacterAppearance = game.ReadNewGameCharacterAppearance(),
            Economy = game.ReadNewGameEconomy(),
            PocketStation = game.ReadNewGamePocketStation()
        };
        ChocoboRewardGrantResult claim = ChocoboRewardTransactions.ClaimRemaining(source);
        Assert.Equal(ChocoboRewardGrantStatus.Granted, claim.Status);
        var taught = catalog.FieldItem(65)!.Use(claim.State.CharacterVitals!, claim.State.Economy!, 65, 16);
        Assert.True(taught.Changed);
        GameState equipped = claim.State with { CharacterVitals = taught.Vitals.SetJunctionAbility(0, 0, 38, true), Economy = taught.Economy };
        BattleCombatantDefinition actor = Assert.Single(BattlePartyProjection.Create(equipped, null, catalog, resources));
        NativeEconomy economy = equipped.Economy!;
        uint price = checked((uint)(100 * actor.Level));
        BattleSession battle = new(new BattleDefinition
        {
            Combatants = [actor with { InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Enemy" }],
            NativeActions = resources.NativeActions.ToArray(),
            NativeCommandNames = resources.ReadNativeCommandNames(),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeGlobalMessages = resources.ReadNativeGlobalBattleMessages(),
            NativeMessageSpeed = 1,
            NativeFieldBattleRules = 0,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativePocketStation = equipped.PocketStation,
            NativeSavedBattleFlags = 0,
            NativeSavedBattleFlagsKnownMask = 255,
            ChocoboWorldMode = equipped.GameplayPreferences.ChocoboWorldMode,
            ChocoboRewards = equipped.ChocoboRewards,
            NativeGil = new(economy.Gil, economy.PastGil),
            GuardianForces = [new(0, 500, 1000)]
        }, BattleAtbMode.Normal);

        BattleActionDefinition choice = Assert.Single(battle.GetNativePlayerActionChoices(actor.Id, false));
        battle.QueueAction(actor.Id, actor.Id, choice.Ability, choice.Id);
        battle.Tick();

        Assert.Equal(10, Assert.Single(battle.Events, entry => entry.Kind == "GuardianForceHealing").Amount);
        Assert.Equal(new NativeBattleGil(economy.Gil - price, economy.PastGil), battle.Capture().NativeGil);
        Assert.Equal((byte)0, battle.Capture().Definition.NativePocketStation!.Flags);
        Assert.DoesNotContain(economy.Items, stock => stock.ItemId == 65);
    }

    [Theory]
    [InlineData("MEMORIA_FF8_GAME_DIR")]
    [InlineData("MEMORIA_FF8_REMASTERED_DIR")]
    public void InstalledExclusiveRewardsTeachAbilitiesAndOpenBokoNaming(string installationVariable)
    {
        using GameAssets assets = ReadInstalled(installationVariable);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        GameState source = new()
        {
            GameplayPreferences = new() { ChocoboWorldMode = ChocoboWorldMode.Memoria },
            CharacterVitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0),
            Economy = assets.ReadNewGameEconomy()
        };
        ChocoboRewardGrantResult claim = ChocoboRewardTransactions.ClaimRemaining(source);
        Assert.Equal(ChocoboRewardGrantStatus.Granted, claim.Status);
        foreach (ChocoboRewardDefinition reward in ChocoboRewardCatalog.Entries.Where(reward => reward.TaughtAbilityId is not null))
        {
            NativeFieldItem metadata = catalog.FieldItem(reward.ItemId)!;
            Assert.Equal(17, metadata.Type);
            Assert.Equal(reward.TaughtAbilityId!.Value, metadata.Power);
            Assert.True(metadata.SupportsGuardianForceUse);
            var taught = metadata.Use(claim.State.CharacterVitals!, claim.State.Economy!, reward.ItemId, 16);
            Assert.True(taught.Changed);
            Assert.True(taught.Vitals.GuardianForces[0].HasLearnedAbility(reward.TaughtAbilityId.Value));
            Assert.DoesNotContain(taught.Economy.Items, stock => stock.ItemId == reward.ItemId);
            GameState consumed = ChocoboRewardTransactions.Reconcile(claim.State with { CharacterVitals = taught.Vitals, Economy = taught.Economy });
            Assert.Contains(reward.Id, consumed.ChocoboRewards.AcquiredUniqueRewardIds);
        }
        NativeFieldItem tag = catalog.FieldItem(165)!;
        Assert.Equal(13, tag.Type);
        Assert.True(tag.SupportsNameUse);
        var naming = tag.BeginNaming(claim.State.CharacterVitals!, claim.State.Economy!, 165, 0);
        Assert.Equal(GameNames.BokoSubject, naming.Subject);
        Assert.DoesNotContain(naming.Economy.Items, stock => stock.ItemId == 165);
        Assert.Contains("chocobos_tag", claim.State.ChocoboRewards.AcquiredUniqueRewardIds);
    }

    private static GameAssets ReadInstalled(string installationVariable)
    {
        string? directory = Environment.GetEnvironmentVariable(installationVariable);
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), $"Set {installationVariable} to run the optional installed item check.");
        return new(directory!);
    }
}
