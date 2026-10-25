using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ChocoboWorldInstalledItemTests
{
    [Theory]
    [InlineData("MEMORIA_FF8_GAME_DIR")]
    [InlineData("MEMORIA_FF8_REMASTERED_DIR")]
    public void InstalledExclusiveRewardsTeachAbilitiesAndOpenBokoNaming(string installationVariable)
    {
        string? directory = Environment.GetEnvironmentVariable(installationVariable);
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), $"Set {installationVariable} to run the optional installed item check.");
        using GameAssets assets = new(directory!);
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
}
