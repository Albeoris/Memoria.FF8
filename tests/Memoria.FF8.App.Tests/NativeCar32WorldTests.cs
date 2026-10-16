using Memoria.FF8.Events;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCar32WorldTests
{
    [Fact]
    public void OriginalType32UsesItsOwnModelFuelAndPersistentProcessOwners()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original car/world resources are required.");
        using GameAssets assets = new(directory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
        session.SetEconomy(session.Economy!.AddItem(162, 1, out _));
        //Declared component inputs through existing canonical owner APIs; this
        //does not fabricate a research checkpoint or claim the train route.
        session.SetWorldEntryMemory(session.WorldEntryMemory!.WriteBytes(256, 2, 455).WriteBytes(264, 1, 8).WriteBytes(265, 1, 1));
        WorldTransition packet = new(61, 32, -1);
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(session.State, directory, packet);
        Assert.Equal((uint)0, session.State.SavedWorld!.FuelDistance112);
        Assert.Equal(74, ticket.InitialSavedWorld!.Slot4.ShapeId);
        Assert.Equal(32, ticket.InitialState.NativeCarRuntime!.EntityType);
        Assert.Equal(9, ticket.InitialState.NativeCarRuntime!.Allocation!.Objects.Length);
        session.CommitPreparedWorldEntry(packet, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
        using WorldMapScene scene = ticket.Adopt(session);
        GameSession independentSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene independent = new(independentSession, directory);
        using WorldAssets worldAssets = new(directory);
        WorldStaticModel ordinary = worldAssets.GetCarModel(32);
        WorldStaticModel exam = worldAssets.GetCarModel(33);
        Assert.Equal((byte)74, ordinary.ShapeId);
        Assert.Equal(10, ordinary.ModelIndex);
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
        Assert.Equal(initial + 32, fuel.PrepareCarFuel(32, 28, 64).FuelDistance112);
        Assert.Equal(initial + 128, fuel.PrepareCarFuel(32, 8, 64).FuelDistance112);
        Assert.Equal(initial + 128, fuel.PrepareCarFuel(32, 27, 64).FuelDistance112);
        Assert.Equal(initial + 64, fuel.PrepareCarFuel(32, 13, 64).FuelDistance112);
        Assert.Equal(initial, fuel.PrepareCarFuel(33, 8, 64).FuelDistance112);
        for (int callback = 0; callback < 1024 && fuel.FuelDistance112 < 0x20000; callback++)
        {
            fuel = fuel.PrepareCarFuel(32, 8, 64);
        }
        Assert.True(fuel.FuelDistance112 >= 0x20000);
        NativeWorldSalaryState threshold = fuel;
        Assert.Throws<NotSupportedException>(() => threshold.PrepareCarFuel(32, 8, 0));
        Assert.Equal(initial, session.State.SavedWorld!.FuelDistance112);

        NativeWorldProcess retained = session.WorldProcess!;
        NativeWorldGroundCacheState ground = scene.State.NativeCarRuntime!.GroundCache!;
        session.StartNewGame();
        Assert.Same(retained, session.WorldProcess);
        NativeCarWorldRuntimeState fresh = NativeCarWorldRuntimeState.Create(session.State.SavedWorld!, ground, session.WorldProcess!);
        Assert.Equal(retained.CarCameraFollowSpeed, fresh.CameraFollowSpeed);
        Assert.Equal((uint)0, session.State.SavedWorld!.FuelDistance112);
    }
}
