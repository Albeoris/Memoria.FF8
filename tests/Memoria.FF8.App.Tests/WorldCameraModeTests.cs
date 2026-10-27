using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Runtime;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class WorldCameraModeTests
{
    [Theory]
    [InlineData((byte)32)]
    [InlineData((byte)33)]
    [InlineData((byte)35)]
    public void EverySupportedCarTogglesOnAnEdgeAndRetainsItsVariantAcrossSnapshot(byte entity)
    {
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(new NativeWorldGroundIdentity(0, 0, 0, 0));
        NativeCarWorldRuntimeState car = new()
        {
            Version = 4,
            Rule = NativeCarWorldRuntimeState.LoadRule,
            EntityType = entity,
            ShapeId = NativeCarWorldRuntimeState.ShapeForEntity(entity),
            Speed = 0,
            CameraFollowSpeed = 0,
            TerrainTiltBlend = 0,
            ModelTilt = 0,
            MapDisplayMode = 0,
            MovementMagnitude20409F4 = 0,
            GroundYaw = 0,
            GroundCache = cache
        };
        WorldState before = new() { Frame = 24, NativeCarRuntime = car };
        WorldState alternate = Toggle(before, new(2, 0, 2));
        Assert.Equal((byte)1, alternate.NativeCarRuntime!.AnimationVariant);
        Assert.Same(alternate, Toggle(alternate, new(2, 2, 3)));
        WorldState restored = JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(alternate))!;
        Assert.Equal((byte)1, restored.NativeCarRuntime!.AnimationVariant);
        Assert.Equal((byte)0, Toggle(restored, new(2, 0, 5)).NativeCarRuntime!.AnimationVariant);
        Assert.Equal(before.CameraYaw, alternate.CameraYaw);
        Assert.Equal(before.CameraVelocity, alternate.CameraVelocity);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    [InlineData((byte)12)]
    [InlineData((byte)14)]
    public void ScriptedTrainAndBoatModesDoNotAdmitTheNativeViewEdge(byte mode)
    {
        WorldState before = new() { Frame = 24, EventUi = NativeWorldEventUi.Create().WithScriptMode(mode) };
        Assert.Same(before, Toggle(before, new(2, 0, 2)));
    }

    [Fact]
    public void OrdinaryFootViewStartsTheActualMultiCallbackPitchAndProjectionTransition()
    {
        NativeFootWorldRuntimeState foot = NativeFootWorldRuntimeState.CreateFirstNormalLoad(NativeSavedWorldState.CreateNewGame());
        WorldState alternate = Toggle(new() { Frame = 24, NativeFootRuntime = foot }, new(2, 0, 2));
        Assert.Equal((byte)1, alternate.NativeFootRuntime!.AnimationVariant);
        Assert.Equal(new NativeFootCameraTransition(17, 1024), alternate.NativeFootRuntime.CameraTransition);
    }

    [Theory]
    [InlineData((byte)32, (short)61, (ushort)455, (byte)1)]
    [InlineData((byte)33, (short)63, (ushort)40, (byte)128)]
    [InlineData((byte)35, (short)3, (ushort)300, (byte)1)]
    public void OriginalCarPacketCameraButtonsOverrideAnalogAndScript14RetainsManualVelocity(byte entity, short spawn, ushort story, byte flags)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld initialization = new(session, TestResources.GameDirectory);
        session.SetWorldEntryMemory(session.WorldEntryMemory!.WriteBytes(256, 2, story).WriteBytes(264, 1, entity == 33 ? 0 : 8).WriteBytes(265, 1, flags));
        session.Restore(session.State with { SavedWorld = session.State.SavedWorld! with { WorldEventFlags116 = NativeWorldEventFlags.CreateNewGame().WithOperand(65).WithOperand(256).WithOperand(260) } });
        WorldTransition packet = new(spawn, entity, -1);
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(session.State, TestResources.GameDirectory, packet);
        session.CommitPreparedWorldEntry(packet, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
        using WorldMapScene scene = ticket.Adopt(session);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 2)).Accepted);
        int yaw = scene.State.CameraYaw;
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 8, CameraHorizontal: -1)).Accepted);
        Assert.True(scene.State.NativeCarRuntime!.ManualCameraVelocity == 8, $"Entity{entity}: variant{scene.State.NativeCarRuntime.AnimationVariant}, script{scene.State.EventUi!.ScriptMode}, velocity{scene.State.NativeCarRuntime.ManualCameraVelocity}, flags{session.State.SavedWorld!.WorldEventFlags116!.Value:X16}");
        Assert.Equal((yaw + 1) & 4095, scene.State.CameraYaw);
        session.Restore(session.State with { World = scene.State with { EventUi = scene.State.EventUi!.WithScriptMode(14) } });
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 4, CameraHorizontal: -1)).Accepted);
        Assert.Equal(8, scene.State.NativeCarRuntime.ManualCameraVelocity);
        Assert.Equal((yaw + 1) & 4095, scene.State.CameraYaw);
    }

    private static WorldState Toggle(WorldState world, NativeWorldInputHistory input)
    {
        MethodInfo method = typeof(WorldMapScene).GetMethod("PrepareFootCameraToggle", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (WorldState)method.Invoke(null, [world, input])!;
    }
}
