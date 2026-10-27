using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Isolated installed-resource components; these are not an acquired-Ragnarok playthrough.</summary>
public sealed class NativeRagnarokSceneTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Fact]
    public void FullMapKeepsFlightMotorAndInputHistoryWithoutChangingAllocationOrCommittingMovement()
    {
        GameSession session = Fixture();
        using WorldMapScene scene = new(session, GameDirectory);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        NativeRagnarokWorldAllocation allocation = scene.State.NativeRagnarokRuntime!.Allocation;
        foreach (byte mode in new byte[] { 1, 2 })
        {
            Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0)).Accepted);
            Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0x100)).Accepted);
            Assert.Equal(mode, WorldMapScene.MapDisplayMode(scene.State));
        }
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0)).Accepted);
        WorldState before = scene.State;
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0x4180)).Accepted);
        Assert.Equal((before.X, before.Z, before.NativeHeight), (scene.State.X, scene.State.Z, scene.State.NativeHeight));
        Assert.Equal((byte)3, scene.State.NativeRagnarokRuntime!.MapDisplayMode);
        Assert.Same(allocation, scene.State.NativeRagnarokRuntime.Allocation);
        Assert.Equal((byte)0, allocation.MapDisplayMode);
        System.Text.Json.Nodes.JsonObject historical = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(session.State))!.AsObject();
        historical["World"]!["NativeRagnarokRuntime"]!.AsObject().Remove("MapDisplayMode");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GameState>(historical.ToJsonString()));
        Assert.True(scene.State.NativeRagnarokRuntime.Motor.Speed > 0);
        Assert.NotEqual(scene.State.NativeHeight, scene.State.NativeRagnarokRuntime.Motor.FlightHeight);
        Assert.Equal((ushort)0x4180, session.State.WorldProcess!.InputHistory!.CurrentButtons);
        Assert.Equal(before.NativeRagnarokRuntime!.Forest!.SeedCounter, scene.State.NativeRagnarokRuntime.Forest!.SeedCounter);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before.NativeRagnarokRuntime.GroundCache), JsonSerializer.SerializeToElement(scene.State.NativeRagnarokRuntime.GroundCache)));
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, GameDirectory);
        PlayerAction held = new(PlayerActionKind.WorldButtons, 0x4180);
        Assert.True(scene.Apply(held).Accepted);
        Assert.True(replay.Apply(held).Accepted);
        Assert.Equal(0, scene.State.NativeRagnarokRuntime.Motor.Speed);
        Assert.Equal(scene.State.NativeHeight, scene.State.NativeRagnarokRuntime.Motor.FlightHeight);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
    }

    [Fact]
    public void OriginalLoaderAndOrdinaryFlightRestoreTheSameNextDigitalCallback()
    {
        GameSession session = Fixture();
        using var scene = new WorldMapScene(session, GameDirectory);
        Assert.NotNull(scene.State.NativeRagnarokRuntime);
        Assert.Null(scene.State.NativeGardenRuntime);
        Assert.Equal(24, scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).FramesAdvanced);
        Assert.Equal(4, scene.Apply(new(PlayerActionKind.WorldButtons, 0x80 | 0x2000 | 0x4000, Frames: 4)).FramesAdvanced);
        Assert.True(scene.State.NativeRagnarokRuntime!.Motor.Speed > 0);
        Assert.NotEqual(scene.State.NativeRagnarokRuntime.Allocation.LoadPose.Yaw, scene.State.NativeRagnarokRuntime.Motor.BodyYaw);
        string path = Path.Combine(Path.GetTempPath(), "Memoria-FF8-ragnarok-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            var restoredSession = new GameSession(GameSession.Load(path));
            using var restored = new WorldMapScene(restoredSession, GameDirectory);
            Assert.Equal(3, scene.Apply(new(PlayerActionKind.WorldButtons, 0x80 | 0x8000, Frames: 3)).FramesAdvanced);
            Assert.Equal(3, restored.Apply(new(PlayerActionKind.WorldButtons, 0x80 | 0x8000, Frames: 3)).FramesAdvanced);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DirectOriginalDeepSeaPortalPublishesIndependentPose2AndFieldPacket()
    {
        GameSession session = Fixture();
        using (var scene = new WorldMapScene(session, GameDirectory))
        {
            scene.Apply(new(PlayerActionKind.Wait, Frames: 24));
            using var assets = new WorldAssets(GameDirectory);
            IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(scene.State.ReplacementMask);
            WorldGroundSelection? selectedGround = null;
            int x = 0;
            int z = 0;
            foreach (WorldTriangle triangle in assets.GetSegment(705, replacements).Triangles.Where(triangle => triangle.FieldEntrance))
            {
                int candidateX = (int)((triangle.A.X + triangle.B.X + triangle.C.X) / 3);
                int candidateZ = (int)((triangle.A.Z + triangle.B.Z + triangle.C.Z) / 3);
                WorldGroundSelection? candidate = assets.LookupNativeGround(candidateX, candidateZ, replacements, []);
                if (candidate is not null && assets.FieldEntries.Select(new(candidateX, candidateZ, 3500, 50), candidate.Ground.Triangle.TextureFlags, (opcode, operand) => opcode == 0xFF20 && operand == 64) is { Entry.Index: 21 })
                {
                    selectedGround = candidate;
                    x = candidateX;
                    z = candidateZ;
                    break;
                }
            }
            Assert.NotNull(selectedGround);
            //Isolated accepted original portal contact; this declares no flight route.
            short height = unchecked((short)(-(int)selectedGround.Ground.Height - 1000));
            NativeRagnarokWorldRuntimeState runtime = scene.State.NativeRagnarokRuntime!;
            NativeWorldFootCameraState camera = runtime.ProjectionCamera! with { X = x, RawZ = z, NativeHeight = height, TargetX = x, TargetRawZ = z, TargetNativeHeight = height - 256 };
            runtime = runtime.PublishBody(new(x, height, -z), 0, checked((short)scene.State.BodyYaw), 0) with { GroundCache = NativeWorldGroundCacheState.CreateFresh(selectedGround.Identity), CurrentGroundHeight = unchecked((short)-(int)selectedGround.Ground.Height), ProjectionCamera = camera, Motor = runtime.Motor with { FlightHeight = height } };
            session.SetWorldState(scene.State with { X = x, Z = z, NativeHeight = height, CameraTargetX = x, CameraTargetZ = z, CameraTargetNativeHeight = height - 256, GroundType = selectedGround.Ground.Triangle.GroundType, NativeRagnarokRuntime = runtime });
        }
        using WorldMapScene world = new(session, GameDirectory);
        world.PrepareFieldEntry = (selection, finalWorld, location, salary) =>
        {
            using GameAssets assets = new(GameDirectory);
            FieldAssets destination = assets.LoadField(selection.Entry.FieldId);
            Assert.NotNull(destination.Information);
            Assert.True(selection.Entry.Triangle < destination.Walkmesh.Triangles.Count);
            FieldTransition transition = new(selection.Entry.FieldId, selection.Entry.X, selection.Entry.Y, selection.Entry.Triangle, selection.Entry.Direction);
            NativeSavedWorldRagnarokReturn receipt = NativeSavedWorldRagnarokReturn.Create(finalWorld, checked((ushort)session.State.FieldId), session.State.WorldEntryMemory!.StoryWord, location);
            GameState departure = session.State;
            return () => session.CommitPreparedRagnarokFieldEntry(departure, finalWorld, transition, receipt, salary, candidate => Assert.Equal(transition, candidate.FieldArrival));
        };
        Assert.True(world.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(848, session.State.FieldId);
        Assert.Equal(new FieldTransition(848, -448, -904, 25, 20), session.State.FieldArrival);
        NativeSavedWorldRagnarokReturn receipt = session.State.SavedWorld!.RagnarokReturn!;
        Assert.Equal(receipt.Pose2, session.State.SavedWorld.RagnarokPose2);
        Assert.Equal((byte)50, session.State.WorldConfiguration!.EntityByte);
        Assert.NotNull(session.State.DepartingWorldState!.NativeRagnarokRuntime);
        Assert.Equal(new NativeSavedWorldPose(0, 0, 0, 0), session.State.SavedWorld.GardenPose3);
    }

    [Fact]
    public void OlderUnknownVerticalOwnerRetainsAWordIntervalWithoutInventingZero()
    {
        GameSession session = Fixture();
        session.Restore(session.State with { WorldProcess = session.State.WorldProcess! with { VerticalBias204DADC = null } });
        using var scene = new WorldMapScene(session, GameDirectory);
        Assert.Null(scene.State.NativeRagnarokRuntime!.Motor.VerticalBias204DADC);
        Assert.Equal(NativeWorldVerticalBiasState.UnknownWord, scene.State.NativeRagnarokRuntime.Motor.VerticalBiasAuthority);
        scene.Apply(new(PlayerActionKind.Wait));
        Assert.Equal(new NativeWorldVerticalBiasState(-32753, 32752), session.State.WorldProcess!.VerticalBias204DADC);
    }

    [Fact]
    public void OriginalRagnarokShapeAndCameraDecodeFromAuthenticatedAssets()
    {
        using var assets = new WorldAssets(GameDirectory);
        FieldModel model = assets.GetRagnarokModel();
        Assert.NotEmpty(model.Vertices);
        Assert.NotEmpty(model.Faces);
        Assert.NotEmpty(model.Animations);
        NativeRagnarokCameraDefaults defaults = assets.ReadRagnarokCameraDefaults();
        defaults.Validate();
        Assert.Equal((short)-341, defaults.OffsetY);
    }

    [Fact]
    public void AlternateViewManualYawAndSaveReceiptRestoreTheSameNextFlightCallback()
    {
        GameSession session = Fixture();
        using WorldMapScene scene = new(session, GameDirectory);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 2)).Accepted);
        Assert.Equal((byte)1, scene.State.NativeRagnarokRuntime!.AnimationVariant);
        Assert.Equal((byte)1, session.State.SavedWorld!.RagnarokReturn!.AnimationVariant108);
        ushort yaw = scene.State.NativeRagnarokRuntime.Motor.CameraYaw;
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 8, CameraHorizontal: -1, Frames: 4)).Accepted);
        Assert.NotEqual(yaw, scene.State.NativeRagnarokRuntime.Motor.CameraYaw);
        Assert.Equal(32, scene.State.NativeRagnarokRuntime.ManualCameraVelocity);
        Assert.Equal(32, session.WorldProcess!.VehicleManualCameraVelocity);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, GameDirectory);
        PlayerAction next = new(PlayerActionKind.WorldButtons, 0x80, CameraHorizontal: -1);
        Assert.True(scene.Apply(next).Accepted);
        Assert.True(replay.Apply(next).Accepted);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 2)).Accepted);
        Assert.Equal((byte)0, scene.State.NativeRagnarokRuntime.AnimationVariant);
    }

    private static GameSession Fixture()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld initialization = new(session, GameDirectory);
        NativeWorldEntryMemory entry = session.State.WorldEntryMemory!.WriteBytes(256, 2, 3500);
        // This declares a component input, not a causal route: actual original
        //spawn12 under a controlled50 packet and the previously-shown tutorial bit.
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new(12, 50, -1), WorldEntryMemory = entry, SavedWorld = session.State.SavedWorld! with { WorldEventFlags116 = NativeWorldEventFlags.CreateNewGame().WithOperand(260) } });
        return session;
    }
}
