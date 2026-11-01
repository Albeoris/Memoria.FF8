using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeRagnarokAllocationAssetsTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Fact]
    public void OriginalLateClauseAllocatesShape1AndIndependentGardenAndEventObjects()
    {
        using WorldAssets assets = new(GameDirectory);
        using GameAssets game = new(GameDirectory);
        NativeRagnarokWorldAllocation allocation = Read(assets, game, 3500, 0, 0);
        Assert.Equal(new byte[] { 0, 2, 1, 64, 65, 90, 91, 94 }, allocation.Objects.Select(item => item.ShapeId));
        Assert.Equal(2, allocation.RagnarokIndex);
        Assert.Equal(new NativeWorldPathPosition(30000, -600, 40000), allocation.Objects[2].Position);
        Assert.Equal(new NativeWorldPathPosition(1024, -128, 2048), allocation.Objects[3].Position);
        Assert.Equal(allocation.Objects[3].Position, allocation.Objects[4].Position);
        Assert.Equal(allocation.Objects[5].Position, allocation.Objects[6].Position);
        Assert.Equal((byte)2, allocation.Objects[4].Flags);
        Assert.Equal((ushort)512, allocation.CameraYaw);
    }

    [Fact]
    public void NativeConditionalAdmissionRetainsDenseIndicesAndPublicationOwners()
    {
        using WorldAssets assets = new(GameDirectory);
        using GameAssets game = new(GameDirectory);
        NativeRagnarokWorldAllocation allocation = Read(assets, game, 3790, 1, 5);
        Assert.Equal(new byte[] { 0, 2, 3, 1, 64, 65, 90, 91, 94, 79, 80, 79, 80 }, allocation.Objects.Select(item => item.ShapeId));
        Assert.Equal(3, allocation.RagnarokIndex);
        NativeRagnarokWorldRuntimeState runtime = NativeRagnarokWorldRuntimeState.CreateAfterLoader(allocation, 24, 112);
        NativeWorldPathPosition newBody = new(31000, -700, 39000);
        NativeRagnarokWorldRuntimeState moved = runtime.PublishBody(newBody, 12, 256, -32);
        Assert.All(moved.Objects.Where(item => item.ShapeId is 0 or 1 or 3), item => Assert.Equal(newBody, item.Position));
        Assert.All(moved.Objects.Select((item, index) => (item, index)).Where(pair => pair.item.ShapeId is not (0 or 1 or 3)), pair => Assert.Equal(allocation.Objects[pair.index], pair.item));
        string json = JsonSerializer.Serialize(moved);
        NativeRagnarokWorldRuntimeState restored = JsonSerializer.Deserialize<NativeRagnarokWorldRuntimeState>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(NativeRagnarokMotor.Step(moved.Motor, 127, 127, -127), NativeRagnarokMotor.Step(restored.Motor, 127, 127, -127));
    }

    [Fact]
    public void OriginalStoryBoundsAndGuardianForceFlagSelectOnlyTheirNativeObjects()
    {
        using WorldAssets assets = new(GameDirectory);
        using GameAssets game = new(GameDirectory);
        Assert.Equal((ushort)3100, Read(assets, game, 3100, 0, 0).Story);
        Assert.Equal((ushort)3899, Read(assets, game, 3899, 0, 0).Story);
        NativeWorldEntryMemory entry = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 3500);
        NativeRagnarokWorldAllocation allocation = assets.ReadRagnarokAllocation(new(62, 50, -1), entry, NativeEventMemory.Empty, NativeSavedWorldState.CreateNewGame(), game.ReadNewGameCharacterVitals().AcquireGuardianForce(13), new(30000, -40000, -600, 256), 512, 0);
        Assert.DoesNotContain(allocation.Objects, item => item.ShapeId == 94);
        Assert.Contains(allocation.Objects, item => item.ShapeId == 90);
        Assert.Contains(allocation.Objects, item => item.ShapeId == 91);
    }

    [Fact]
    public void ReloadRefusesUnknownPoseCameraVariantAndUnreachedStoryClauses()
    {
        using WorldAssets assets = new(GameDirectory);
        using GameAssets game = new(GameDirectory);
        NativeWorldEntryMemory entry = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 3500);
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame();
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        Assert.Throws<NotSupportedException>(() => assets.ReadRagnarokAllocation(new(62, 50, -1), entry, NativeEventMemory.Empty, saved, vitals));
        Assert.Throws<NotSupportedException>(() => assets.ReadRagnarokAllocation(new(62, 50, -1), entry, NativeEventMemory.Empty, saved, vitals, new(1, 2, -300, 0), 0));
        Assert.Throws<NotSupportedException>(() => Read(assets, game, 3900, 0, 0));
        Assert.Throws<NotSupportedException>(() => Read(assets, game, 3099, 0, 0));
        NativeRagnarokWorldAllocation relocated = assets.ReadRagnarokAllocation(new(62, 50, -1), entry, NativeEventMemory.Empty.Write(269, 1, NativeScalarValue.Exact(1)), saved, vitals, new(1, 2, -300, 0), 0, 0);
        Assert.Equal((short)9, relocated.Objects.Single(item => item.ShapeId == 64).Yaw);
    }

    [Theory]
    [InlineData(74)]
    [InlineData(85)]
    [InlineData(87)]
    public void SavedCarIsAppendedBeforeOptionalObjectsAndSurvivesIndependentShipMovement(byte shape)
    {
        using WorldAssets assets = new(GameDirectory);
        using GameAssets game = new(GameDirectory);
        NativeWorldEntryMemory entry = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 3500).WriteBytes(264, 1, 5);
        NativeSavedWorldVehicleSlot slot = new(shape, new(-21894, -41257, -172, 3008));
        NativeSavedWorldVehicleSlot inactive = new(86, new(12345, -23456, -300, 512));
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame() with { GardenPose3 = new(1024, -2048, -128, 128), Slot4 = slot, Slot5 = inactive };
        NativeRagnarokWorldAllocation allocation = assets.ReadRagnarokAllocation(new(62, 50, -1), entry, NativeEventMemory.Empty, saved, game.ReadNewGameCharacterVitals(), new(30000, -40000, -600, 256), 512, 0);
        Assert.Equal(slot, allocation.SavedCarSlot);
        Assert.Equal(shape, allocation.Objects[allocation.SavedCarIndex].ShapeId);
        Assert.Equal(new byte[] { 0, 2, 1, 64, 65, 90, 91, shape, 94, 79, 80, 79, 80 }, allocation.Objects.Select(item => item.ShapeId));
        NativeRagnarokWorldRuntimeState runtime = NativeRagnarokWorldRuntimeState.CreateAfterLoader(allocation, 24, 112);
        NativeRagnarokWorldRuntimeState flying = runtime.PublishBody(new(31000, -700, 39000), 12, 256, -32);
        Assert.Equal(runtime.Objects[allocation.SavedCarIndex], flying.Objects[allocation.SavedCarIndex]);
        NativeRagnarokWorldRuntimeState driving = flying.PublishCarBody(new(-21000, -172, 41000), 1024);
        driving.Validate();
        Assert.Equal(flying.Objects[allocation.RagnarokIndex], driving.Objects[allocation.RagnarokIndex]);
        Assert.Equal(new NativeWorldPathPosition(-21000, -172, 41000), driving.Objects[allocation.SavedCarIndex].Position);
        NativeRagnarokWorldRuntimeState restored = JsonSerializer.Deserialize<NativeRagnarokWorldRuntimeState>(JsonSerializer.Serialize(driving))!;
        restored.Validate();
        Assert.Equal(driving.Objects[allocation.SavedCarIndex], restored.Objects[allocation.SavedCarIndex]);
        Assert.Equal(inactive, saved.Slot5);
    }

    [Fact]
    public void ActualCarPacketKeepsDistinctSavedShipAndGardenPoses()
    {
        using WorldAssets assets = new(GameDirectory);
        using GameAssets game = new(GameDirectory);
        NativeWorldEntryMemory entry = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 3200);
        NativeSavedWorldPose ship = new(23456, -34567, -400, 1024);
        NativeSavedWorldPose garden = new(12345, -23456, -300, 512);
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame() with { RagnarokPose2 = ship, GardenPose3 = garden, Slot4 = new(85, new(1000, -2000, -100, 0)), Slot5 = new(86, new(2000, -3000, -200, 128)) };
        WorldTransition packet = new(3, 38, -1);
        NativeSavedWorldPose car = new(assets.Spawns[3].X, assets.Spawns[3].Z, assets.Spawns[3].NativeHeight, (ushort)((assets.Spawns[3].Unknown >> 8) * 16));
        NativeRagnarokWorldAllocation allocation = assets.ReadRagnarokCarAllocation(packet, entry, NativeEventMemory.Empty, saved, game.ReadNewGameCharacterVitals().AcquireGuardianForce(13), car, 0, 0);
        allocation.Validate();
        assets.ValidateRagnarokAllocationResources(allocation);
        Assert.Equal(packet, allocation.Packet);
        Assert.Equal((byte)38, allocation.ControlledEntityType);
        Assert.Equal(new NativeWorldPathPosition(ship.X, ship.Height, -ship.RawZ), allocation.Objects[allocation.RagnarokIndex].Position);
        Assert.Equal(new NativeWorldPathPosition(garden.X, garden.Height, -garden.RawZ), allocation.Objects.Single(item => item.ShapeId == 64).Position);
        Assert.Equal(new NativeWorldPathPosition(car.X, car.Height, -car.RawZ), allocation.Objects[allocation.SavedCarIndex].Position);
        Assert.Equal(saved.Slot4, allocation.SavedCarSlot);
    }

    private static NativeRagnarokWorldAllocation Read(WorldAssets assets, GameAssets game, ushort story, byte chocobo, byte replacements)
    {
        NativeWorldEntryMemory entry = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, story).WriteBytes(264, 1, replacements);
        NativeEventMemory memory = NativeEventMemory.Empty.Write(1536, 1, NativeScalarValue.Exact(chocobo));
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame() with { GardenPose3 = new(1024, -2048, -128, 128) };
        return assets.ReadRagnarokAllocation(new(62, 50, -1), entry, memory, saved, game.ReadNewGameCharacterVitals(), new(30000, -40000, -600, 256), 512, 0);
    }
}
