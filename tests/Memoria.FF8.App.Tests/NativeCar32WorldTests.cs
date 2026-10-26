using Memoria.FF8.Events;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCar32WorldTests
{
    [Theory]
    [InlineData(32, 74, 10, 61, 455)]
    [InlineData(35, 82, 18, 3, 300)]
    public void OrdinaryCarsUseTheirOwnModelFuelAndPersistentProcessOwners(byte entityType, byte shape, int modelIndex, short entry, ushort story)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original car/world resources are required.");
        using GameAssets assets = new(directory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        session.SetEconomy(session.Economy!.AddItem(162, 1, out _));
        //Declared component inputs through existing canonical owner APIs; this
        //does not fabricate a research checkpoint or claim a gameplay route.
        session.SetWorldEntryMemory(session.WorldEntryMemory!.WriteBytes(256, 2, story).WriteBytes(264, 1, 8).WriteBytes(265, 1, 1));
        WorldTransition packet = new(entry, entityType, -1);
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(session.State, directory, packet);
        Assert.Equal((uint)0, session.State.SavedWorld!.FuelDistance112);
        Assert.Equal(shape, ticket.InitialSavedWorld!.Slot4.ShapeId);
        Assert.Equal(entityType, ticket.InitialState.NativeCarRuntime!.EntityType);
        Assert.NotEmpty(ticket.InitialState.NativeCarRuntime!.Allocation!.Objects);
        if (entityType == 35)
        {
            WorldState spawn = ticket.InitialState;
            Assert.Equal((-15639, -39437, -172, 2256, 3296), (spawn.X, spawn.Z, spawn.NativeHeight, spawn.CameraYaw, spawn.BodyYaw));
        }
        else
        {
            Assert.Equal(9, ticket.InitialState.NativeCarRuntime.Allocation.Objects.Length);
        }
        session.CommitPreparedWorldEntry(packet, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
        using WorldMapScene scene = ticket.Adopt(session);
        GameSession independentSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene independent = new(independentSession, directory);
        using WorldAssets worldAssets = new(directory);
        WorldStaticModel ordinary = worldAssets.GetCarModel(entityType);
        WorldStaticModel exam = worldAssets.GetCarModel(33);
        Assert.Equal(shape, ordinary.ShapeId);
        Assert.Equal(modelIndex, ordinary.ModelIndex);
        NativeWorldContactShape dimensions = worldAssets.ReadNativeContactShape(shape);
        Assert.Equal(entityType == 35 ? new NativeWorldContactShape(417, 176, 200, 1) : new NativeWorldContactShape(453, 202, 120, 1), dimensions);
        Assert.Equal(entityType == 35 ? 208 : 226, dimensions.Length >> 1);
        Assert.Equal((byte)73, exam.ShapeId);
        Assert.Equal(9, exam.ModelIndex);
        Assert.Equal(84, exam.NativeVertices.Count);
        Assert.Equal(58, exam.Faces.Count);
        Assert.Equal(2, exam.Textures.Count);

        void Step(PlayerAction action)
        {
            WorldState before = scene.State;
            Assert.Equal(scene.Apply(action), independent.Apply(action));
            NativeWorldFootCameraState camera = scene.State.NativeCarRuntime!.ProjectionCamera!;
            Assert.Equal((before.X, before.Z, before.NativeHeight), (camera.X, camera.RawZ, camera.NativeHeight));
            Assert.Equal(scene.State.NativeCarRuntime!.CameraFollowSpeed, session.WorldProcess!.CarCameraFollowSpeed);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(independentSession.State)));
        }
        for (int callback = 0; callback < 12; callback++)
        {
            Step(new PlayerAction(PlayerActionKind.Move, Vertical: -1));
        }
        Assert.Equal(24, scene.State.NativeCarRuntime!.Speed);
        Assert.True(scene.State.NativeCarRuntime!.MovementMagnitude20409F4 > 0);
        Assert.Equal(-24, session.WorldProcess!.CameraCooldown);
        Assert.True(session.State.SavedWorld!.FuelDistance112 > 0);
        session.SetEconomy(session.Economy!.AddItem(162, -1, out _));
        independentSession.SetEconomy(independentSession.Economy!.AddItem(162, -1, out _));
        uint noFuelDistance = session.State.SavedWorld!.FuelDistance112!.Value;
        Step(new PlayerAction(PlayerActionKind.Wait));
        Assert.Equal(20, scene.State.NativeCarRuntime!.Speed);
        Step(new PlayerAction(PlayerActionKind.Move, Vertical: -1));
        Assert.Equal(16, scene.State.NativeCarRuntime!.Speed);
        Assert.Equal(noFuelDistance, session.State.SavedWorld!.FuelDistance112);
        Step(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
        Assert.Equal(-36, session.WorldProcess!.CameraCooldown);
        Assert.Equal(scene.Render(320, 224), independent.Render(320, 224));

        session.SetEconomy(session.Economy!.AddItem(162, 1, out _));
        NativeWorldSalaryState fuel = NativeWorldSalaryState.Begin(session.State);
        uint initial = fuel.FuelDistance112!.Value;
        Assert.Equal(initial + 32, fuel.PrepareCarFuel(entityType, 28, 64).FuelDistance112);
        Assert.Equal(initial + 128, fuel.PrepareCarFuel(entityType, 8, 64).FuelDistance112);
        Assert.Equal(initial + 128, fuel.PrepareCarFuel(entityType, 27, 64).FuelDistance112);
        Assert.Equal(initial + 64, fuel.PrepareCarFuel(entityType, 13, 64).FuelDistance112);
        Assert.Equal(initial, fuel.PrepareCarFuel(33, 8, 64).FuelDistance112);
        for (int callback = 0; callback < 1024 && fuel.FuelDistance112 < 0x20000; callback++)
        {
            fuel = fuel.PrepareCarFuel(entityType, 8, 64);
        }
        Assert.True(fuel.FuelDistance112 >= 0x20000);
        NativeWorldSalaryState threshold = fuel;
        NativeWorldSalaryState consumed = threshold.PrepareCarFuel(entityType, 8, 0, worldAssets.ReadNativeFuelMessage);
        Assert.Equal((uint)0, consumed.FuelDistance112);
        Assert.Equal(0, consumed.Economy!.Items.Single(item => item.ItemId == 162).Count);
        Assert.Equal(43, consumed.EventUi!.Windows.Single().MessageId);
        Assert.Equal(initial, session.State.SavedWorld!.FuelDistance112);

        foreach (byte quantity in new byte[] { 0, 1, 2, 10, 100 })
        {
            NativeEconomy economy = session.Economy!.DeepCopy();
            int slot = Array.FindIndex(economy.Items, item => item.ItemId == 162);
            economy.Items[slot] = economy.Items[slot] with { Count = quantity };
            GameState boundary = session.State with { Economy = economy, SavedWorld = session.State.SavedWorld! with { FuelDistance112 = 0x20000 } };
            if (quantity == 1)
            {
                GameSession batchedSession = new(boundary);
                GameSession singleSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(boundary))!);
                using WorldMapScene batchedScene = new(batchedSession, directory);
                using WorldMapScene singleScene = new(singleSession, directory);
                Assert.Equal(1, batchedScene.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1, Frames: 2)).FramesAdvanced);
                singleScene.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1));
                Assert.Equal(JsonSerializer.Serialize(singleSession.State), JsonSerializer.Serialize(batchedSession.State));
                batchedScene.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1));
                Assert.Equal(16, batchedSession.State.World!.NativeCarRuntime!.Speed);
            }
            GameSession consumingSession = new(boundary);
            using WorldMapScene consumingScene = new(consumingSession, directory);
            consumingScene.Apply(new PlayerAction(PlayerActionKind.Wait));
            Assert.Equal((uint)0, consumingSession.State.SavedWorld!.FuelDistance112);
            Assert.Equal(Math.Max(0, quantity - 1), consumingSession.Economy!.Items[slot].Count);
            Assert.Equal(30, consumingSession.WorldProcess!.FuelMessageMovementCallbacks2045C78);
            Assert.NotNull(consumingScene.CurrentDialogue);
            Assert.Equal(worldAssets.ReadFuelMessage((byte)Math.Max(0, quantity - 1)), consumingScene.CurrentDialogue.Text);
            GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(consumingSession.State))!);
            using WorldMapScene restoredScene = new(restoredSession, directory);
            Assert.Equal(consumingScene.CurrentDialogue, restoredScene.CurrentDialogue);
            for (int callback = 0; callback < 32; callback++)
            {
                PlayerAction move = new(PlayerActionKind.Move, Vertical: -1);
                Assert.Equal(consumingScene.Apply(move), restoredScene.Apply(move));
                Assert.Equal(consumingScene.CurrentDialogue, restoredScene.CurrentDialogue);
            }
            if (consumingSession.WorldProcess.FuelMessageMovementCallbacks2045C78 == 0)
            {
                Assert.DoesNotContain(consumingSession.State.World!.EventUi!.Windows, window => window.WindowId == 1);
            }
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(consumingSession.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        }

        NativeSavedWorldCarReturn receipt = NativeSavedWorldCarReturn.Create(scene.State, 317, story, scene.PlaceId);
        Assert.Equal(entityType, receipt.EntityType105);
        NativeSavedWorldState returned = session.State.SavedWorld!.WithCarReturn(receipt);
        Assert.Equal(255, returned.Slot4.ShapeId);
        Assert.Equal(receipt.Pose, returned.Slot4.Pose);
        Assert.Equal(receipt, JsonSerializer.Deserialize<NativeSavedWorldState>(JsonSerializer.Serialize(returned))!.CarReturn);
        NativeWorldConfiguration configuration = NativeWorldConfiguration.FromWorldReturn(scene.State, 317);
        configuration.Validate();
        Assert.Equal(entityType, configuration.EntityByte);
        Assert.Throws<InvalidDataException>(() => (scene.State.NativeCarRuntime! with { ShapeId = 73 }).Validate());

        NativeWorldProcess retained = session.WorldProcess!;
        NativeWorldGroundCacheState ground = scene.State.NativeCarRuntime!.GroundCache!;
        NativeCarWorldRuntimeState fresh = NativeCarWorldRuntimeState.Create(session.State.SavedWorld!, ground, session.WorldProcess!);
        Assert.Equal(retained.CarCameraFollowSpeed, fresh.CameraFollowSpeed);
        Assert.Equal(initial, session.State.SavedWorld!.FuelDistance112);
    }
}
