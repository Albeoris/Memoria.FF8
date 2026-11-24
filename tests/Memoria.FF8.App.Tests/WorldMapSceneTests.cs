using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

/// <summary>Selected original-world component fixtures; these do not advance or establish a natural player exit.</summary>
public sealed class WorldMapSceneTests
{
    [Theory]
    [InlineData(33, true, false)]
    [InlineData(34, false, true)]
    [InlineData(35, true, true)]
    public void WorldInventoryUsesRestItemsWithoutTheFieldSaveBit(int itemId, bool healsCharacters, bool healsGuardianForces)
    {
        string directory = GameDirectory();
        var session = ComponentSession();
        using var scene = new WorldMapScene(session, directory);
        using var assets = new GameAssets(directory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = session.State.CharacterVitals!.WithSavedHitPoints(0, 1).AcquireGuardianForce(0);
        vitals.Records[0] = vitals.Records[0] with { Status = 0x82 };
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { HitPoints = 1 };
        NativeEconomy economy = session.State.Economy!.AddItem(itemId, 2, out _);
        GameState initial = session.State with { CharacterVitals = vitals, Economy = economy, WorldControls = session.State.WorldControls! with { Menu209 = 2 } };
        session.Restore(initial);

        string menuItemId = $"item-{itemId}";
        GameState field = initial with { Scene = SceneKind.Field };
        Assert.False(LiveMenuData.Build(field, catalog: catalog).Items.Single(item => item.Id == menuItemId).NativeFieldUseSupported);
        GameState savePoint = field with { WorldControls = field.WorldControls! with { Menu209 = 1 } };
        Assert.True(LiveMenuData.Build(savePoint, catalog: catalog).Items.Single(item => item.Id == menuItemId).NativeFieldUseSupported);

        int beforeCount = economy.Items.Single(item => item.ItemId == itemId).Count;
        int uses = 0;
        MenuController menu = new(() => LiveMenuData.Build(session.State, catalog: catalog), action =>
        {
            Assert.Equal("item-use", action.Kind);
            Assert.Equal(menuItemId, action.SubjectId);
            Assert.Equal("character-0", action.TargetId);
            PlayerActionResult result = scene.UseFieldItem(new(PlayerActionKind.UseFieldItem, itemId, Quantity: 0), catalog.FieldItem(itemId)!, assets.ReadFieldItemStatRules);
            Assert.True(result.Accepted);
            Assert.Equal(0, result.FramesAdvanced);
            uses++;
        });
        menu.Open("item-use", false);
        Assert.True(menu.Entries.Single(entry => entry.Id == menuItemId).Enabled);
        menu.SelectEntry("item-use", menuItemId);
        menu.Navigate(MenuInput.Confirm);
        menu.Navigate(MenuInput.Confirm);

        Assert.Equal(1, uses);
        NativeCharacterVitals healed = session.State.CharacterVitals!;
        Assert.Equal(healsCharacters ? vitals.GetMagicMaximumHitPoints(0) : 1, healed.Records[0].HitPoints.Minimum);
        Assert.Equal(healsCharacters ? 0x80 : 0x82, healed.Records[0].Status);
        Assert.Equal(healsGuardianForces ? vitals.Definitions.GuardianForceMaximum(0, vitals.GuardianForces[0]) : 1, healed.GuardianForces[0].HitPoints);
        Assert.Equal(beforeCount - 1, session.State.Economy!.Items.Single(item => item.ItemId == itemId).Count);
        Assert.Equal(initial.World!.Frame, scene.State.Frame);

        scene.UseFieldItem(new(PlayerActionKind.UseFieldItem, itemId, Quantity: 0), catalog.FieldItem(itemId)!, assets.ReadFieldItemStatRules);
        Assert.Equal(beforeCount - 1, session.State.Economy!.Items.Single(item => item.ItemId == itemId).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void World_callbacks_preserve_hidden_or_displayed_timer_and_recompute_the_same_native_motion(bool displayed)
    {
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld initialization = new(session, GameDirectory());
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0) });
        NativeExamTimerState timer = NativeExamTimerState.FromReachedSet(600).Display(250, 10);
        if (!displayed)
        {
            timer = timer.KillDisplay();
        }
        session.SetExamTimer(timer);
        using var scene = new WorldMapScene(session, GameDirectory());
        GameState initial = session.State;
        Assert.Equal(0, session.ExamTimer!.ClockCalls);
        Assert.Equal(3, scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3)).FramesAdvanced);
        NativeExamTimerState expected = timer.AdvanceCallbacks(NativeTimerCallbackKind.World, 3);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(session.ExamTimer));
        var replaySession = new GameSession(initial);
        using var replay = new WorldMapScene(replaySession, GameDirectory());
        Assert.Equal(3, replay.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3)).FramesAdvanced);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(replaySession.State)));
        Assert.False(scene.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(session.ExamTimer));
    }

    [Fact]
    public void Refused_world_batch_preserves_timer_before_any_candidate_callback_is_committed()
    {
        var session = ComponentSession();
        session.SetExamTimer(NativeExamTimerState.FromReachedSet(600).Display(250, 10).KillDisplay());
        using var scene = new WorldMapScene(session, GameDirectory());
        session.SetWorldState(scene.State with { X = -5000, CameraTargetX = -1476, CameraYaw = 0, CameraVelocity = 0 });
        GameState before = session.State;
        Assert.Throws<NotSupportedException>(() => scene.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 2)));
        Assert.Same(before, session.State);
        Assert.Equal(0, session.ExamTimer!.ClockCalls);
    }

    [Fact]
    public void OriginalSpawnAndFirstGroundUpdatePreserveNativeHeightRounding()
    {
        string directory = GameDirectory();
        var session = ComponentSession();
        using var scene = new WorldMapScene(session, directory);
        Assert.Equal(30.5, WorldMapScene.SimulationFps);
        Assert.Equal((24576, -29406, -658), (scene.State.X, scene.State.Z, scene.State.NativeHeight));
        Assert.Equal((176, 2048), (scene.State.CameraYaw, scene.State.BodyYaw));
        Assert.Equal((24576, -29406, -914), (scene.State.CameraTargetX, scene.State.CameraTargetZ, scene.State.CameraTargetNativeHeight));
        Assert.Equal((byte)28, scene.State.GroundType);
        Assert.Equal((ushort)285, scene.State.ReplacementMask);
        Assert.Empty(session.State.Variables);

        PlayerActionResult result = scene.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.True(result.Accepted);
        Assert.Equal(1, result.FramesAdvanced);
        Assert.Equal((24576, -29406, -657), (scene.State.X, scene.State.Z, scene.State.NativeHeight));
        Assert.Equal(0, scene.State.AnimationId);
        Assert.Equal(1, scene.State.Frame);
    }

    [Fact]
    public void CardinalMovementUsesOriginalFiveGroundProbesAndPreCollisionCameraTarget()
    {
        string directory = GameDirectory();
        var session = ComponentSession();
        using var scene = new WorldMapScene(session, directory);
        Assert.Equal(24, scene.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 24)).FramesAdvanced);
        Assert.Equal((24576, -29406), (scene.State.X, scene.State.Z));
        session.SetWorldState(scene.State with { CameraYaw = 0, BodyYaw = 1024 });

        PlayerActionResult move = scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
        Assert.True(move.Accepted);
        Assert.Equal((24607, -29406, -657), (scene.State.X, scene.State.Z, scene.State.NativeHeight));
        Assert.Equal((1, 0), (scene.State.AnimationId, scene.State.AnimationCounter));
        Assert.Equal((1, 8), (scene.State.CameraYaw, scene.State.CameraVelocity));
        Assert.Equal(24576, scene.State.CameraTargetX);
        Assert.Equal((byte)28, scene.State.GroundType);

        scene.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.Equal((0, 0), (scene.State.AnimationId, scene.State.AnimationCounter));
        Assert.Equal(24591, scene.State.CameraTargetX);
        Assert.Equal((1, 6), (scene.State.CameraYaw, scene.State.CameraVelocity));
    }

    [Fact]
    public void SelectedWorldStateSaveReloadPreservesCameraAnimationActionsAndPixels()
    {
        string directory = GameDirectory();
        string checkpoint = Path.Combine(Path.GetTempPath(), "Memoria.FF8-world-component-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var originalSession = ComponentSession();
            using var original = new WorldMapScene(originalSession, directory);
            original.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3));
            original.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 2));
            originalSession.Save(checkpoint);

            var resumedSession = new GameSession(GameSession.Load(checkpoint));
            using var resumed = new WorldMapScene(resumedSession, directory);
            Assert.Equal(JsonSerializer.Serialize(original.State), JsonSerializer.Serialize(resumed.State));
            Assert.Equal(originalSession.State.WorldArrival, resumedSession.State.WorldArrival);
            Assert.Empty(resumedSession.State.Variables);

            var next = new PlayerAction(PlayerActionKind.Move, Vertical: -1, Frames: 2);
            Assert.Equal(original.Apply(next), resumed.Apply(next));
            Assert.Equal(JsonSerializer.Serialize(original.State), JsonSerializer.Serialize(resumed.State));
            byte[] pixels = original.Render(320, 224);
            Assert.Equal(320 * 224 * 4, pixels.Length);
            Assert.Equal(pixels, resumed.Render(320, 224));
            Assert.Contains(pixels.Where((value, index) => index % 4 != 3), value => value != 0);
        }
        finally
        {
            File.Delete(checkpoint);
        }
    }

    [Fact]
    public void Unproved_first_ground_candidate_refuses_before_any_world_tick_or_salary_result()
    {
        string directory = GameDirectory();
        var session = ComponentSession();
        using var scene = new WorldMapScene(session, directory);
        session.SetWorldState(scene.State with { X = -5000, CameraTargetX = -1476, CameraYaw = 0, CameraVelocity = 0 });
        string before = JsonSerializer.Serialize(session.State);
        Assert.Throws<NotSupportedException>(() => scene.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 2)));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
    }

    [Fact]
    public void SuccessfulBatchMatchesIndividualTicksWithOneRecordedAction()
    {
        string directory = GameDirectory();
        var batchSession = ComponentSession();
        using var batch = new WorldMapScene(batchSession, directory);
        var singleSession = new GameSession(batchSession.State);
        using var single = new WorldMapScene(singleSession, directory);
        Assert.Equal(3, batch.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3)).FramesAdvanced);
        for (int tick = 0; tick < 3; tick++)
        {
            Assert.True(single.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).Accepted);
        }
        Assert.Equal(JsonSerializer.Serialize(single.State), JsonSerializer.Serialize(batch.State));
    }

    [Fact]
    public void ContactRegionRefusalPreservesAllWorldStateAndActions()
    {
        var session = ComponentSession();
        using var scene = new WorldMapScene(session, GameDirectory());
        session.SetWorldState(scene.State with { X = -5000, CameraTargetX = -3238, CameraYaw = 0, CameraVelocity = 0 });
        string before = JsonSerializer.Serialize(session.State);
        Assert.Throws<NotSupportedException>(() => scene.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
    }

    [Fact]
    public void UnsupportedEntityTypeIsRejectedBeforeLoadingWorldAssets()
    {
        var session = new GameSession(new GameState { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 1, 0) });
        Assert.Throws<NotSupportedException>(() => new WorldMapScene(session, "missing world corpus"));
    }

    private static GameSession ComponentSession()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld initialization = new(session, GameDirectory());
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0) });
        return session;
    }

    private static string GameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed world corpus is required for this isolated component fixture.");
        return directory;
    }
}
