using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeFootWorldReloadTests
{

    [Fact]
    public void ActualFirstLoaderAuxiliariesSurviveOrdinaryActionsAndRestore()
    {
        var session = new GameSession(FirstEntry());
        using var scene = new WorldMapScene(session, GameDirectory());
        NativeFootWorldRuntimeState runtime = Assert.IsType<NativeFootWorldRuntimeState>(scene.State.NativeFootRuntime);
        Assert.Equal(NativeFootWorldRuntimeState.FirstLoadRule, runtime.Rule);
        scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3));
        scene.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 2));
        Assert.Equal(runtime.Rule, scene.State.NativeFootRuntime!.Rule);
        Assert.Equal(runtime.AnimationVariant, scene.State.NativeFootRuntime.AnimationVariant);
        Assert.Equal(runtime.MapDisplayMode, scene.State.NativeFootRuntime.MapDisplayMode);
        Assert.Equal(runtime.Forest!.SeedCounter, scene.State.NativeFootRuntime.Forest!.SeedCounter);
        var restoredSession = new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using var restored = new WorldMapScene(restoredSession, GameDirectory());
        Assert.Equal(JsonSerializer.Serialize(scene.State.NativeFootRuntime), JsonSerializer.Serialize(restored.State.NativeFootRuntime));
        Assert.Equal(scene.Render(320, 224), restored.Render(320, 224));
        var next = new PlayerAction(PlayerActionKind.Move, Vertical: -1, Frames: 2);
        Assert.Equal(scene.Apply(next), restored.Apply(next));
        Assert.Equal(JsonSerializer.Serialize(scene.State), JsonSerializer.Serialize(restored.State));
    }

    [Fact]
    public void UniformFootComponentLoadsOriginalBalambSpawnAndRestoresItsSelectedModelAndTicks()
    {
        // Isolated structural loader fixture; this establishes no live journey or live journey.
        GameState entry = FirstEntry() with
        {
            WorldArrival = new WorldTransition(1, 1, -1),
            EventMemory = NativeEventMemory.Empty,
            WorldEntryMemory = NativeWorldEntryMemory.CreateCleared().WriteBytes(256, 2, 135),
            SavedWorld = NativeSavedWorldState.CreateNewGame() with { Slot4 = new NativeSavedWorldVehicleSlot(255, new NativeSavedWorldPose(12884, -26807, -304, 2699)) }
        };
        var session = new GameSession(entry);
        using var scene = new WorldMapScene(session, GameDirectory());
        Assert.Equal((13249, -26779, -304), (scene.State.X, scene.State.Z, scene.State.NativeHeight));
        Assert.Equal((byte)1, scene.State.NativeFootRuntime!.EntityType);
        Assert.Equal(0, scene.State.Frame);
        var restoredSession = new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using var restored = new WorldMapScene(restoredSession, GameDirectory());
        Assert.Equal(scene.Render(320, 224), restored.Render(320, 224));
        PlayerAction action = new(PlayerActionKind.Move, Vertical: -1, Frames: 2);
        Assert.Equal(scene.Apply(action), restored.Apply(action));
        Assert.Equal(JsonSerializer.Serialize(scene.State), JsonSerializer.Serialize(restored.State));
        Assert.Equal(2, scene.State.Frame);
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordFootDeparture(scene.State, 106, 135, 1);
        Assert.Equal((byte)1, receipt.EntityType105);
        Assert.Equal((byte)1, NativeFootWorldRuntimeState.CreateSavedReload(receipt).EntityType);
    }

    [Fact]
    public void Entry62RestoresUnquantizedSavedPoseYawsAndPreservesItsReceipt()
    {
        var initialSession = new GameSession(FirstEntry());
        using var first = new WorldMapScene(initialSession, GameDirectory());
        // Isolated loader fixture derived from the actual spawn, deliberately
        // using yaws that the ordinary twelve-byte spawn cannot encode.
        WorldState departure = first.State with { Frame = 1650, BodyYaw = 165, CameraYaw = 199 };
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordFootDeparture(departure, 159, 17, 1);
        GameState field = FirstEntry() with { Scene = SceneKind.Field, FieldId = 136, WorldArrival = null, SavedWorld = initialSession.State.SavedWorld!.WithFootReturn(receipt) };
        string before = JsonSerializer.Serialize(field);
        using PreparedWorldEntry prepared = WorldEntryPreflight.Prepare(field, GameDirectory(), new WorldTransition(62, 0, -1));
        WorldState loaded = prepared.InitialState;
        Assert.Equal((departure.X, departure.Z, departure.NativeHeight, 165, 199), (loaded.X, loaded.Z, loaded.NativeHeight, loaded.BodyYaw, loaded.CameraYaw));
        Assert.Equal((loaded.X, loaded.Z, loaded.NativeHeight - 256), (loaded.CameraTargetX, loaded.CameraTargetZ, loaded.CameraTargetNativeHeight));
        Assert.Equal((0, 0, 0L), (loaded.CameraVelocity, loaded.AnimationCounter, loaded.Frame));
        Assert.Equal(1650, loaded.NativeFootRuntime!.ReloadSourceWorldFrame);
        Assert.Equal(before, JsonSerializer.Serialize(field));
        GameState candidate = field with { Scene = SceneKind.WorldMap, WorldArrival = prepared.Transition, World = loaded };
        var session = new GameSession(candidate);
        using WorldMapScene reloaded = prepared.Adopt(session);
        var restoredSession = new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(candidate))!);
        using var restored = new WorldMapScene(restoredSession, GameDirectory());
        Assert.Equal(reloaded.Render(320, 224), restored.Render(320, 224));
        var next = new PlayerAction(PlayerActionKind.Wait);
        Assert.Equal(reloaded.Apply(next), restored.Apply(next));
        Assert.Equal(JsonSerializer.Serialize(reloaded.State), JsonSerializer.Serialize(restored.State));
        Assert.Equal(receipt, session.State.SavedWorld!.FootReturn);
    }

    [Fact]
    public void BattleMode2ReloadUsesItsActualMode3DepartureAndRestoresWithoutAnEntry62Packet()
    {
        // Original-resource loader component; this does not reconstruct or claim a live battle journey.
        GameSession initial = new(FirstEntry());
        using WorldMapScene first = new(initial, GameDirectory());
        first.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 2));
        WorldState departure = first.State;
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordBattleDeparture(departure, 159, 17, first.PlaceId);
        GameState input = initial.State with { World = null, DepartingWorldState = departure, SavedWorld = initial.State.SavedWorld!.WithFootReturn(receipt) };
        GameSession reloadedSession = new(input);
        using WorldMapScene reloaded = new(reloadedSession, GameDirectory(), battleReload: receipt);
        Assert.Equal((byte)3, receipt.WorldMode);
        Assert.Equal(input.WorldArrival, reloadedSession.State.WorldArrival);
        Assert.Equal(NativeFootWorldRuntimeState.BattleReloadRule, reloaded.State.NativeFootRuntime!.Rule);
        Assert.Equal((departure.X, departure.Z, departure.BodyYaw, departure.CameraYaw), (reloaded.State.X, reloaded.State.Z, reloaded.State.BodyYaw, reloaded.State.CameraYaw));
        Assert.Equal(0, reloaded.State.Frame);
        GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(reloadedSession.State))!);
        using WorldMapScene restored = new(restoredSession, GameDirectory());
        Assert.Equal(reloaded.Render(320, 224), restored.Render(320, 224));
        PlayerAction next = new(PlayerActionKind.Wait);
        Assert.Equal(reloaded.Apply(next), restored.Apply(next));
        Assert.Equal(JsonSerializer.Serialize(reloaded.State), JsonSerializer.Serialize(restored.State));
    }

    [Fact]
    public void HistoricalWorldCannotInventEntityTypeOrFootReceiptAcrossRestore()
    {
        var originalSession = new GameSession(FirstEntry());
        using var original = new WorldMapScene(originalSession, GameDirectory());
        WorldState historical = original.State with { NativeFootRuntime = null };
        Assert.Throws<NotSupportedException>(() => NativeSavedWorldFootReturn.RecordFootDeparture(historical, 159, 17, 1));
        var oldSession = new GameSession(originalSession.State with { World = historical });
        Assert.Throws<NotSupportedException>(() => new WorldMapScene(oldSession, "missing corpus"));
        Assert.Throws<NotSupportedException>(() => new WorldMapScene(new GameSession(originalSession.State with { WorldArrival = new WorldTransition(62, 0, 0) }), "missing corpus"));
    }

    [Theory]
    [InlineData(1385)]
    [InlineData(1386)]
    [InlineData(1387)]
    [InlineData(1388)]
    [InlineData(1389)]
    [InlineData(1390)]
    public void LegacyAuxiliaryAliasesRefuseBeforeResourcesWithoutMutatingDeparture(int address)
    {
        GameState field = FirstEntry() with { Scene = SceneKind.Field, EventMemory = NativeEventMemory.FromDWords(new Dictionary<int, int> { [address] = 0 }) };
        string before = JsonSerializer.Serialize(field);
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(field, "missing corpus", new WorldTransition(0, 0, 0)));
        Assert.Equal(before, JsonSerializer.Serialize(field));
    }

    private static GameState FirstEntry()
    {
        using Memoria.FF8.Assets.GameAssets assets = new(GameDirectory());
        // Declared original-resource loader component, with actual InitOut and executed cold/New Game owner factories.
        return GameSession.CreateColdProcess().State with
        {
            Scene = SceneKind.WorldMap,
            WorldArrival = new WorldTransition(0, 0, -1),
            SavedWorld = NativeSavedWorldState.CreateNewGame(),
            WorldControls = NativeWorldControlState.CreateNewGame(),
            WorldProcess = NativeWorldProcess.FromColdProcess(),
            WorldEntryMemory = NativeWorldEntryMemory.CreateCleared().WriteBytes(256, 2, 17),
            SeeD = NativeSeeDState.CreateNewGame() with { SalaryTable = assets.ReadNativeSalaryTable() },
            EventRandom = NativeEventRandomState.FromColdProcess().ClearSavedStateForNewGame(),
            GlobalRandom = assets.ReadNewGameGlobalRandom(),
            CharacterVitals = assets.ReadNewGameCharacterVitals(),
            CharacterAppearance = assets.ReadNewGameCharacterAppearance(),
            Economy = assets.ReadNewGameEconomy(),
            InputConfiguration = assets.ReadNewGameInputConfiguration(),
            CardInventory = NativeCardInventory.CreateNewGame(),
            ShopHistory = assets.ReadNewGameShopHistory(),
            WeaponRecipes = assets.ReadNewGameWeaponRecipes(),
            ShotAvailability = assets.ReadNewGameShotAvailability(),
            LimitProgress = assets.ReadNewGameLimitProgress(),
            SavedBattleFlags = assets.ReadNewGameSavedBattleFlags(),
            KnownMagic = assets.ReadNewGameKnownMagic(),
            ScannedModels = assets.ReadNewGameScannedModels()
        };
    }

    private static string GameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed world corpus is required.");
        return directory;
    }
}
