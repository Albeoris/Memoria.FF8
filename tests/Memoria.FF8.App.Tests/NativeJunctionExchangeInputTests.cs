using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeJunctionExchangeInputTests
{
    [Fact]
    public void ExplicitNativeRowsRoundTripThroughTheStableControlContract()
    {
        MenuAction menu = new("junction-exchange", SubjectId: "character-3", TargetId: "character-5");
        Assert.True(NativeJunctionMenuProjection.IsAction(menu));
        PlayerAction action = NativeJunctionMenuProjection.PlayerAction(menu);
        Assert.Equal(40, (int)PlayerActionKind.ExchangeJunctions);
        Assert.Equal(39, (int)PlayerActionKind.ApplyJunctionEdit);
        Assert.Equal(new PlayerAction(PlayerActionKind.ExchangeJunctions, 3, "5"), action);
        Assert.True(NativeJunctionMenuInput.IsMenuAction(action.Kind));
        PlayerAction restored = JsonSerializer.Deserialize<PlayerAction>(JsonSerializer.Serialize(action))!;
        NativeJunctionMenuInput.Validate(restored);
        Assert.Equal(action, restored);
    }

    [Theory]
    [InlineData("", "character-5")]
    [InlineData("character-3", "")]
    [InlineData("3", "character-5")]
    [InlineData("character-3", "gf-5")]
    [InlineData("character--1", "character-5")]
    [InlineData("character-8", "character-5")]
    [InlineData("character-3", "character-8")]
    [InlineData("character-3", "character-3")]
    [InlineData("character- 3", "character-5")]
    [InlineData("character-3", "character-5 ")]
    public void ProjectionRejectsMissingMalformedOrNonDistinctCharacterIdentities(string source, string target)
    {
        Assert.Throws<ArgumentException>(() => NativeJunctionMenuProjection.PlayerAction(new("junction-exchange", SubjectId: source, TargetId: target)));
    }

    [Fact]
    public void InputRejectsInvalidRowsAndAnyOperandsOutsideOneOrdinaryMenuCallback()
    {
        PlayerAction action = new(PlayerActionKind.ExchangeJunctions, 3, "5");
        PlayerAction[] malformed =
        [
            action with { Value = -1 },
            action with { Value = 8 },
            action with { Text = null },
            action with { Text = "" },
            action with { Text = "-1" },
            action with { Text = "8" },
            action with { Text = "3" },
            action with { Text = "character-5" },
            action with { Text = "+5" },
            action with { Text = " 5" },
            action with { Text = "5 " },
            action with { Quantity = 0 },
            action with { Frames = 0 },
            action with { Frames = 2 },
            action with { Horizontal = 1 },
            action with { Vertical = 1 },
            action with { CameraHorizontal = 1 },
            action with { WorldButtons = 1 },
            action with { WaitWhilePlayable = true }
        ];
        Assert.All(malformed, input => Assert.Throws<ArgumentException>(() => NativeJunctionMenuInput.Validate(input)));
        NativeJunctionMenuInput.Validate(new(PlayerActionKind.ExchangeJunctions, 0, "7"));
        NativeJunctionMenuInput.Validate(new(PlayerActionKind.ExchangeJunctions, 7, "0"));
    }

    [Fact]
    public void SwitchNavigationSuppliesTheSelectedSourceAndReturnsAfterOneLiveAction()
    {
        MenuData data = SwitchData();
        data.SupportedActions.Add("junction-exchange");
        List<MenuAction> actions = [];
        MenuController controller = new(() => data, actions.Add);
        controller.Open("switch");
        controller.SelectEntry("switch", "switch-junction");
        controller.Navigate(MenuInput.Confirm);
        controller.SelectEntry("switch-junction", "character-3");
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("switch-junction-target", controller.ScreenId);
        Assert.Equal("character-3", controller.ExchangeId);
        Assert.Equal("character-0", controller.Character!.Id);
        Assert.False(controller.Entries.Single(entry => entry.Id == "character-3").Enabled);
        Assert.True(controller.Entries.Single(entry => entry.Id == "character-5").Enabled);
        Assert.Throws<InvalidOperationException>(() => controller.SelectEntry("switch-junction-target", "character-3"));
        Assert.Empty(actions);
        controller.SelectEntry("switch-junction-target", "character-5");
        controller.Navigate(MenuInput.Confirm);
        MenuAction applied = Assert.Single(actions);
        Assert.Equal(new MenuAction("junction-exchange", SubjectId: "character-3", TargetId: "character-5"), applied);
        Assert.Equal(new PlayerAction(PlayerActionKind.ExchangeJunctions, 3, "5"), NativeJunctionMenuProjection.PlayerAction(applied));
        Assert.Equal("switch-junction", controller.ScreenId);
        controller.Navigate(MenuInput.Cancel);
        Assert.Equal("switch", controller.ScreenId);
    }

    [Fact]
    public void UnsupportedLiveExchangeDisablesEveryTargetAndDoesNotDispatch()
    {
        MenuData data = SwitchData();
        List<MenuAction> actions = [];
        MenuController controller = new(() => data, actions.Add);
        controller.Open("switch-junction");
        controller.SelectEntry("switch-junction", "character-3");
        controller.Navigate(MenuInput.Confirm);
        Assert.All(controller.Entries, entry => Assert.False(entry.Enabled));
        controller.Navigate(MenuInput.Confirm);
        Assert.Empty(actions);
        Assert.Equal("switch-junction-target", controller.ScreenId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HpWarningDefaultsToNoAndCancellationKeepsTheTargetSelection(bool cancelButton)
    {
        MenuData data = SwitchData();
        data.SupportedActions.Add("junction-exchange");
        List<(string Source, string Target)> previews = [];
        data.JunctionExchangeLowersHp = (source, target) =>
        {
            previews.Add((source, target));
            return true;
        };
        List<MenuAction> actions = [];
        MenuController controller = OpenExchangeWarning(data, actions);
        Assert.Equal("no", controller.SelectedEntry!.Id);
        Assert.Equal(("character-3", "character-5"), Assert.Single(previews));
        Assert.Empty(actions);
        controller.Navigate(cancelButton ? MenuInput.Cancel : MenuInput.Confirm);
        Assert.Empty(actions);
        Assert.Equal("switch-junction-target", controller.ScreenId);
        Assert.Equal("character-5", controller.SelectedEntry!.Id);
        controller.Execute(new("switch-junction-confirm"));
        Assert.Empty(actions);
    }

    [Fact]
    public void AcceptingHpWarningDispatchesOnceAndReturnsToTheSourceSelection()
    {
        MenuData data = SwitchData();
        data.SupportedActions.Add("junction-exchange");
        data.JunctionExchangeLowersHp = (_, _) => true;
        List<MenuAction> actions = [];
        MenuController controller = OpenExchangeWarning(data, actions);
        controller.SelectEntry("switch-junction-confirm", "yes");
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(new MenuAction("junction-exchange", SubjectId: "character-3", TargetId: "character-5"), Assert.Single(actions));
        Assert.Equal("switch-junction", controller.ScreenId);
        controller.Execute(new("switch-junction-confirm"));
        Assert.Single(actions);
        controller.Navigate(MenuInput.Cancel);
        Assert.Equal("switch", controller.ScreenId);
    }

    [Theory]
    [InlineData("source-changed")]
    [InlineData("source-removed")]
    [InlineData("target-removed")]
    [InlineData("support-removed")]
    public void HpWarningDoesNotDispatchWhenThePendingEndpointsBecomeStale(string change)
    {
        MenuData data = SwitchData();
        data.SupportedActions.Add("junction-exchange");
        data.JunctionExchangeLowersHp = (_, _) => true;
        List<MenuAction> actions = [];
        MenuController controller = OpenExchangeWarning(data, actions);
        switch (change)
        {
            case "source-changed":
                controller.Execute(new("select-exchange", SubjectId: "character-0"));
                break;
            case "source-removed":
                data.Characters.RemoveAll(character => character.Id == "character-3");
                break;
            case "target-removed":
                data.Characters.RemoveAll(character => character.Id == "character-5");
                break;
            case "support-removed":
                data.SupportedActions.Remove("junction-exchange");
                break;
        }
        controller.Execute(new("switch-junction-confirm"));
        Assert.Empty(actions);
        Assert.Equal("switch-junction-target", controller.ScreenId);
    }

    [Theory]
    [InlineData(SceneKind.Field)]
    [InlineData(SceneKind.WorldMap)]
    public void InstalledLiveDataExposesExchangeAndTheProjectedInputSwapsSavedPackages(SceneKind scene)
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals source = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(3, 0);
        source = source.ChangeMagicStock(3, 1, 40, MagicOperationReason.ScriptReward, out _);
        source = source.ChangeMagicStock(5, 2, 60, MagicOperationReason.ScriptReward, out _);
        string original = JsonSerializer.Serialize(source);
        MenuData data = LiveMenuData.Build(new GameState { Scene = scene, CharacterVitals = source }, catalog: LiveMenuCatalog.ReadInstalled(assets));
        Assert.Contains("junction-exchange", data.SupportedActions);
        Assert.NotNull(data.JunctionExchangeLowersHp);
        data.JunctionExchangeLowersHp("character-3", "character-5");
        Assert.Equal(original, JsonSerializer.Serialize(source));
        PlayerAction action = NativeJunctionMenuProjection.PlayerAction(new("junction-exchange", SubjectId: "character-3", TargetId: "character-5"));
        NativeCharacterVitals result = NativeJunctionMenuInput.Apply(source, action);
        Assert.Equal(source.Records[5].Junction, result.Records[3].Junction);
        Assert.Equal(source.Records[3].Junction, result.Records[5].Junction);
        Assert.Equal(source.Records[5].MagicInventory, result.Records[3].MagicInventory);
        Assert.Equal(source.Records[3].MagicInventory, result.Records[5].MagicInventory);
        Assert.Equal(original, JsonSerializer.Serialize(source));
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(result))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(restored));
    }

    private static MenuController OpenExchangeWarning(MenuData data, List<MenuAction> actions)
    {
        MenuController controller = new(() => data, actions.Add);
        controller.Open("switch");
        controller.SelectEntry("switch", "switch-junction");
        controller.Navigate(MenuInput.Confirm);
        controller.SelectEntry("switch-junction", "character-3");
        controller.Navigate(MenuInput.Confirm);
        controller.SelectEntry("switch-junction-target", "character-5");
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("switch-junction-confirm", controller.ScreenId);
        return controller;
    }

    private static MenuData SwitchData()
    {
        MenuData data = new();
        foreach (int row in new[] { 0, 3, 5 })
        {
            data.Characters.Add(new MenuCharacter { Id = $"character-{row}", NativeId = row, Name = $"Character {row}", InParty = row == 0, CurrentHp = 100, MaximumHp = 100 });
        }
        return data;
    }

    private static GameAssets InstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original GF/kernel resources are required.");
        return new GameAssets(directory);
    }
}
