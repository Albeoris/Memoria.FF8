using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeGfReassignmentInputTests
{
    [Fact]
    public void HiddenSavedOwnerStillProjectsItsIdentityAndTransfersAfterConfirmation()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original GF/kernel resources are required.");
        using GameAssets assets = new(directory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(3, 0);
        NativeCharacterVital[] records = vitals.Records.ToArray();
        records[3] = records[3] with { MemberFlags = 0 };
        vitals = vitals with { Records = records, CombatParty = [0, 255, 255] };
        List<PlayerAction> actions = [];
        MenuController controller = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = vitals }, catalog: catalog), action =>
        {
            PlayerAction input = NativeJunctionMenuProjection.PlayerAction(action);
            actions.Add(input);
            vitals = NativeJunctionMenuInput.Apply(vitals, input);
        });
        controller.Open("junction");
        Assert.DoesNotContain(controller.Data.Characters, character => character.Id == "character-3");
        MenuGuardianForceOwner owner = Assert.IsType<MenuGuardianForceOwner>(controller.Data.GuardianForces.Single(force => force.Id == "gf-0").Owner);
        Assert.Equal("character-3", owner.Id);
        Assert.Equal((int?)records[3].CharacterId, owner.NativeId);
        MenuJunctionPlan plan = controller.Data.JunctionPlanner!.Plan(new("junction-gf-reassign", "gf-0", "character-0", owner.Id));
        Assert.True(plan.HasChanges, plan.UnavailableReason);
        controller.Execute(new("junction-ui", "gf-portrait-gf-0"));
        Assert.Equal("junction-dashboard", controller.ScreenId);
        Assert.Equal("junction-transfer-no", controller.BuildView().FocusedElement?.Id);
        Assert.Empty(actions);
        controller.Execute(new("junction-ui", "junction-transfer-yes"));
        Assert.Equal(NativeJunctionMenuProjection.PlayerAction(plan.Action), Assert.Single(actions));
        Assert.DoesNotContain(0, vitals.Records[3].Junction.GuardianForceAssignments);
        Assert.Contains(0, vitals.Records[0].Junction.GuardianForceAssignments);
        Assert.Equal("character-0", controller.Data.GuardianForces.Single(force => force.Id == "gf-0").Owner?.Id);
    }

    [Fact]
    public void ConfirmationProjectsExplicitGfDestinationAndPreviousOwnerIntoOneSerializableAction()
    {
        MenuAction menu = new("junction-gf-reassign", "gf-15", "character-7", "character-3");
        Assert.True(NativeJunctionMenuProjection.IsAction(menu));
        PlayerAction action = NativeJunctionMenuProjection.PlayerAction(menu);
        Assert.Equal(new PlayerAction(PlayerActionKind.ReassignGuardianForce, 7, "15", Quantity: 3), action);
        Assert.True(NativeJunctionMenuInput.IsMenuAction(action.Kind));
        PlayerAction restored = JsonSerializer.Deserialize<PlayerAction>(JsonSerializer.Serialize(action))!;
        NativeJunctionMenuInput.Validate(restored);
        Assert.Equal(action, restored);
        Assert.Equal(PlayerActionKind.JunctionGuardianForce, NativeJunctionMenuProjection.PlayerAction(new MenuAction("junction-gf", "gf-15", "character-7")).Kind);
    }

    [Theory]
    [InlineData("gf-16", "character-1", "character-0")]
    [InlineData("gf-0", "character-11", "character-0")]
    [InlineData("gf-0", "character-1", "character-11")]
    [InlineData("gf-0", "character-1", "")]
    [InlineData("gf-0", "character-1", "character-1")]
    [InlineData("0", "character-1", "character-0")]
    public void ConfirmationProjectionRejectsMissingOrInvalidIdentities(string gf, string destination, string owner)
    {
        Assert.Throws<ArgumentException>(() => NativeJunctionMenuProjection.PlayerAction(new MenuAction("junction-gf-reassign", gf, destination, owner)));
    }

    [Theory]
    [InlineData(-1, "0", 0)]
    [InlineData(NativeCharacterVitals.ExtendedRecordCount, "0", 0)]
    [InlineData(1, "-1", 0)]
    [InlineData(1, "16", 0)]
    [InlineData(1, "gf-0", 0)]
    [InlineData(1, "0", null)]
    [InlineData(1, "0", -1)]
    [InlineData(1, "0", NativeCharacterVitals.ExtendedRecordCount)]
    [InlineData(1, "0", 1)]
    public void TransferInputRequiresExplicitDistinctNativeOwnersAndActualGf(int destination, string gf, int? owner)
    {
        Assert.Throws<ArgumentException>(() => NativeJunctionMenuInput.Validate(new PlayerAction(PlayerActionKind.ReassignGuardianForce, destination, gf, Quantity: owner)));
    }

    [Fact]
    public void TransferInputRejectsMovementAndWaitOperands()
    {
        PlayerAction action = new(PlayerActionKind.ReassignGuardianForce, 1, "0", Quantity: 0);
        PlayerAction[] malformed = [action with { Frames = 2 }, action with { Horizontal = 1 }, action with { Vertical = 1 }, action with { CameraHorizontal = 1 }, action with { WorldButtons = 1 }, action with { WaitWhilePlayable = true }];
        Assert.All(malformed, input => Assert.Throws<ArgumentException>(() => NativeJunctionMenuInput.Validate(input)));
    }
}
