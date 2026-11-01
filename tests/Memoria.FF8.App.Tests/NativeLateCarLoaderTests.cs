using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeLateCarLoaderTests
{
    [Theory]
    [InlineData(750, 0)]
    [InlineData(750, 1)]
    [InlineData(3200, 0)]
    [InlineData(3200, 1)]
    public void FreshCarPacketRetainsItsActualLateAllocationAndParkedVehicles(ushort story, byte flags265)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original car and world resources are required.");
        using GameAssets assets = new(directory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        session.SetEconomy(session.Economy!.AddItem(162, 1, out _));
        session.SetWorldEntryMemory(session.WorldEntryMemory!.WriteBytes(256, 2, story).WriteBytes(264, 1, 8).WriteBytes(265, 1, flags265));
        NativeSavedWorldPose gardenPose = new(12345, -23456, -300, 512);
        NativeSavedWorldPose ragnarokPose = new(23456, -34567, -400, 1024);
        session.SetSavedWorldState(session.State.SavedWorld! with { GardenPose3 = gardenPose, RagnarokPose2 = ragnarokPose, WorldEventFlags116 = NativeWorldEventFlags.CreateNewGame().WithOperand(256) });
        WorldTransition packet = new(3, 38, -1);
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(session.State, directory, packet);
        NativeCarWorldRuntimeState initial = ticket.InitialState.NativeCarRuntime!;
        Assert.Equal((byte)38, initial.EntityType);
        Assert.Equal((byte)85, initial.ShapeId);
        Assert.Null(initial.Allocation);
        Assert.Null(initial.ObjectProcess);
        int selectedCarIndex = initial.GardenObjects?.Allocation.SavedCarIndex ?? initial.RagnarokObjects!.Allocation.SavedCarIndex;
        Assert.True(selectedCarIndex >= 0);
        Assert.Throws<InvalidDataException>(() => (initial with { PrimaryContact = -1, BroadContact = selectedCarIndex }).Validate());
        Assert.Throws<InvalidDataException>(() => (initial with { PrimaryContact = selectedCarIndex, BroadContact = selectedCarIndex }).Validate());
        NativeWorldBackgroundRailState initialRails = initial.GardenObjects?.BackgroundRails ?? initial.RagnarokObjects!.BackgroundRails!;
        initialRails.Validate();
        Assert.Equal(flags265, initialRails.LoadFlags265);
        Assert.Equal(0, initialRails.Callback);
        Assert.Equal(flags265 == 0, initialRails.HasBanks);
        Assert.Null(initialRails.DistancePosition);
        Assert.NotEqual(new NativeWorldPathPosition(gardenPose.X, gardenPose.Height, -gardenPose.RawZ), new(ticket.InitialState.X, ticket.InitialState.NativeHeight, -ticket.InitialState.Z));
        if (story == 750)
        {
            NativeGardenWorldRuntimeState garden = Assert.IsType<NativeGardenWorldRuntimeState>(initial.GardenObjects);
            Assert.Equal(packet, garden.Allocation.Packet);
            Assert.Equal(new NativeWorldPathPosition(gardenPose.X, gardenPose.Height, -gardenPose.RawZ), garden.Objects[garden.Allocation.Garden64Index].Position);
            Assert.Equal(new NativeWorldPathPosition(ticket.InitialState.X, ticket.InitialState.NativeHeight, -ticket.InitialState.Z), garden.Objects[garden.Allocation.SavedCarIndex].Position);
        }
        else
        {
            NativeRagnarokWorldRuntimeState ragnarok = Assert.IsType<NativeRagnarokWorldRuntimeState>(initial.RagnarokObjects);
            Assert.Equal(packet, ragnarok.Allocation.Packet);
            Assert.Equal(new NativeWorldPathPosition(ragnarokPose.X, ragnarokPose.Height, -ragnarokPose.RawZ), ragnarok.Objects[ragnarok.Allocation.RagnarokIndex].Position);
            Assert.Equal(new NativeWorldPathPosition(ticket.InitialState.X, ticket.InitialState.NativeHeight, -ticket.InitialState.Z), ragnarok.Objects[ragnarok.Allocation.SavedCarIndex].Position);
            Assert.Equal(new NativeWorldPathPosition(gardenPose.X, gardenPose.Height, -gardenPose.RawZ), ragnarok.Objects.Single(item => item.ShapeId == 64).Position);
        }
        session.CommitPreparedWorldEntry(packet, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
        using WorldMapScene scene = ticket.Adopt(session);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, directory);
        PlayerAction move = new(PlayerActionKind.Move, Vertical: -1, Frames: 2);
        Assert.Equal(scene.Apply(move), replay.Apply(move));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
        Assert.Equal(scene.Render(160, 120), replay.Render(160, 120));
        Assert.Equal((byte)38, scene.State.NativeCarRuntime!.EntityType);
        NativeWorldBackgroundRailState continuedRails = scene.State.NativeCarRuntime.GardenObjects?.BackgroundRails ?? scene.State.NativeCarRuntime.RagnarokObjects!.BackgroundRails!;
        Assert.Equal(2, continuedRails.Callback);
        Assert.NotNull(continuedRails.DistancePosition);
        foreach (NativeTrainRailBank bank in continuedRails.Banks)
        {
            Assert.Equal(28, bank.Pause);
            Assert.All(bank.Presentation, pose => Assert.NotNull(pose.Projection));
        }
        Assert.NotEqual((0, 0), (scene.State.X - ticket.InitialState.X, scene.State.Z - ticket.InitialState.Z));
    }
}
