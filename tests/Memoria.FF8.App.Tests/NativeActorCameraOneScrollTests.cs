using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeActorCameraOneScrollTests
{
    [Fact]
    public void ActualMemoryActorRowOneFollowsThenCosineRetargetsWithSelectedCameraAuthority()
    {
        string source = TestResources.FixtureFile("../fixtures/external-fixture.json");
        Assert.SkipUnless(File.Exists(source), "The genuine memory DSCROLLA2 boundary snapshot is required.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(source));
        JsonElement checkpoint = document.RootElement.GetProperty("EventState");
        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        SceneEventState scene = JsonSerializer.Deserialize<SceneEventState>(checkpoint.GetProperty("Scene"), options)!;
        ActorState[] actors = JsonSerializer.Deserialize<ActorState[]>(checkpoint.GetProperty("Actors"), options)!;
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldAssets field = assets.LoadField(882);
        Assert.Null(scene.ActorBackgroundScroll);
        Assert.Equal(1, scene.CameraIndex);
        Assert.Equal(((short)80, (short)0), (scene.NormalBackgroundScrollCenter!.X, scene.NormalBackgroundScrollCenter.Y));
        (short X, short Y)? projected = ActorBackgroundScrollCertificate.Resolve(field, scene, actors, 6, 1);
        Assert.True(projected.HasValue);
        (short X, short Y) center = projected.Value;
        JsonNode next = JsonSerializer.SerializeToNode(scene)!;
        next.AsObject().Remove("NormalBackgroundScrollCenter");
        next["ActorBackgroundScroll"] = JsonSerializer.SerializeToNode(new NativeActorBackgroundScroll(1, 6, 6, 6, 1, center.X, center.Y, 80, 0, Layer: 1));
        SceneEventState following = next.Deserialize<SceneEventState>(options)!;
        Assert.True(ActorBackgroundScrollCertificate.CanRestore(field, following, actors));
        Assert.Equal(new Vector2(-center.X, -center.Y), ActorBackgroundScrollCertificate.Prepare(field, following)!.ActorOffset);
        Assert.Null(DirectBackgroundScrollCertificate.ResolveNormalCenter(field, following, actors));
        Assert.True(ActorBackgroundScrollCertificate.CanSetInterpolated(field, following, actors, 1, 80, 1));
        (short X, short Y) target = ActorBackgroundScrollCertificate.Resolve(field, following, actors, 1, 1)!.Value;
        foreach (short elapsed in new short[] { 0, 40, 80 })
        {
            short x = NativeActorScrollPhase.Interpolate(2, center.X, target.X, 80, elapsed);
            short y = NativeActorScrollPhase.Interpolate(2, center.Y, target.Y, 80, elapsed);
            next["ActorBackgroundScroll"] = JsonSerializer.SerializeToNode(new NativeActorBackgroundScroll(1, 1, 1, 1, elapsed == 80 ? (byte)2 : (byte)1, x, y, center.X, center.Y, Mode: 2, Duration: 80, Counter: elapsed == 80 ? elapsed : (short)(elapsed + 1), ProjectedTargetX: target.X, ProjectedTargetY: target.Y, Layer: 1));
            SceneEventState phase = next.Deserialize<SceneEventState>(options)!;
            Assert.True(ActorBackgroundScrollCertificate.CanRestore(field, phase, actors));
            SceneEventState restored = JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(phase), options)!;
            Assert.True(ActorBackgroundScrollCertificate.CanRestore(field, restored, actors));
            Assert.Equal(new Vector2(-x, -y), ActorBackgroundScrollCertificate.Prepare(field, restored)!.ActorOffset);
            Assert.Null(DirectBackgroundScrollCertificate.ResolveNormalCenter(field, restored, actors));
        }
        next["CameraIndex"] = 0;
        SceneEventState cameraZero = next.Deserialize<SceneEventState>(options)!;
        Assert.True(ActorBackgroundScrollCertificate.CanRestore(field, cameraZero, actors));
        Assert.Null(ActorBackgroundScrollCertificate.Prepare(field, cameraZero));
        Assert.NotNull(DirectBackgroundScrollCertificate.ResolveNormalCenter(field, cameraZero, actors));
        //The later helper selects CA0 without resetting physical row1. Give the
        //same metadata a layer1 tile to verify its retained current subtraction.
        FieldAssets layered = field with { Background = [.. field.Background, field.Background[0] with { Layer = 1 }] };
        BackgroundScrollFrame frame = NormalCameraCertificate.Prepare(layered, cameraZero, actors)!;
        if (field.Information!.BackgroundWrapMask == 0)
        {
            Assert.Equal(new Vector2(-target.X, -target.Y) + field.GetCamera(0).Pan, frame.LayerOffsets[1]);
        }
        else
        {
            Assert.NotNull(frame.NativeWrap);
        }
        Assert.Equal((byte)2, cameraZero.ActorBackgroundScroll!.Status);
        Assert.Equal(1, cameraZero.ActorBackgroundScroll.Layer);
    }

    [Fact]
    public void NonselectedContinuousActorRowKeepsTheIndependentNonzeroCameraOneCenter()
    {
        byte[] script = [5, 6];
        FieldCamera camera0 = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new(90, -10, 3896), Vector2.Zero, 640);
        FieldCamera camera1 = camera0 with { Translation = new(300, -10, 3896) };
        FieldInformation information = new(0, 200, Enumerable.Repeat(new FieldBounds(-512, 512, 512, -512), 8).ToArray(), [new(0, 224, 320, 0)], [], []);
        FieldAssets field = new(600, "synthetic-independent-camera-records", script, [], [], camera0, new([]), [new FieldModel("synthetic", [], [], [], [], [])]) { Cameras = [camera0, camera1], Information = information };
        NativeFieldInitializationProof proof = new(1, NativeFieldRuntimeProfile.FF8EnExecutableHash, field.Id, Convert.ToHexString(SHA256.HashData(script)), 20, NativeFieldInitializationStage.Completed);
        SceneEventState scene = Scene(new { AudioLoadEpoch = 1, NativeLayerZeroScrollCurrent = new NativeLayerZeroScrollCurrent(1, 0, 0), CameraIndex = 1, NativeFieldInitialization = proof, NativeFieldLoadVersion = 1, NativeActorLayoutVersion = 1, NativeActorObjectIds = new[] { 0, 1 }, FieldPartyNativeActorIds = new[] { 0, 255, 255 }, ActorBackgroundScroll = new NativeActorBackgroundScroll(1, 1, 1, 1, 1, 0, 0, 7, -28) });
        ActorState leader = Actor(0, -45, -170);
        ActorState target = Actor(1, -80, 110);
        ActorState[] actors = [leader, target];
        Assert.Equal(((short)0, (short)0), ActorBackgroundScrollCertificate.Resolve(field, scene, actors, 1));
        Assert.Null(ActorBackgroundScrollCertificate.Prepare(field, scene));
        Assert.True(ActorBackgroundScrollCertificate.CanRestore(field, scene, actors));
        Assert.False(ActorBackgroundScrollCertificate.CanSetInterpolated(field, scene, actors, 1, 10));
        BackgroundScrollFrame selected = NormalCameraCertificate.Prepare(field, scene, actors)!;
        FieldNativeCameraCenter leaderCenter = NormalCameraCertificate.ProjectActorCenter(field, scene, leader).Center!.Value;
        FieldNativeCameraCenter targetCenter = NormalCameraCertificate.ProjectActorCenter(field, scene, target).Center!.Value;
        Assert.NotEqual(0, leaderCenter.X);
        Assert.NotEqual(leaderCenter, targetCenter);
        Assert.Equal(new Vector2(-leaderCenter.X, -leaderCenter.Y), selected.ActorOffset);
        Assert.Equal(((short)leaderCenter.X, (short)leaderCenter.Y), DirectBackgroundScrollCertificate.ResolveNormalCenter(field, scene, actors));
        JsonNode malformed = JsonSerializer.SerializeToNode(scene)!;
        malformed["ActorBackgroundScroll"]!["X"] = 1;
        Assert.False(ActorBackgroundScrollCertificate.CanRestore(field, malformed.Deserialize<SceneEventState>()!, actors));
    }

    [Fact]
    public void ActualCourtBoundaryAdmitsItsRetainedRawActorWithoutReplacingSelectedCameraOne()
    {
        string source = TestResources.FixtureFile("../fixtures/external-fixture.json");
        Assert.SkipUnless(File.Exists(source), "The genuine court boundary snapshot is required.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(source));
        JsonElement checkpoint = document.RootElement.GetProperty("EventState");
        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        SceneEventState scene = JsonSerializer.Deserialize<SceneEventState>(checkpoint.GetProperty("Scene"), options)!;
        ActorState[] actors = JsonSerializer.Deserialize<ActorState[]>(checkpoint.GetProperty("Actors"), options)!;
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldAssets field = assets.LoadField(879);
        Assert.Equal(1, scene.CameraIndex);
        Assert.Equal(((short)0, (short)0), ActorBackgroundScrollCertificate.Resolve(field, scene, actors, 4));
        JsonNode after = JsonSerializer.SerializeToNode(scene)!;
        after["BackgroundScrollPositions"]!.AsObject().Remove("0");
        after["ActorBackgroundScroll"] = JsonSerializer.SerializeToNode(new NativeActorBackgroundScroll(1, 4, 4, 4, 1, 0, 0, 0, 0));
        SceneEventState following = after.Deserialize<SceneEventState>(options)!;
        Assert.True(ActorBackgroundScrollCertificate.CanRestore(field, following, actors));
        Assert.Null(ActorBackgroundScrollCertificate.Prepare(field, following));
        BackgroundScrollFrame selected = NormalCameraCertificate.Prepare(field, following, actors)!;
        ActorState leader = NativeFieldPartyBinding.ResolveActor(following, actors, 0)!;
        FieldNativeCameraCenter center = NormalCameraCertificate.ProjectActorCenter(field, following, leader).Center!.Value;
        Assert.Equal(new Vector2(-center.X, -center.Y), selected.ActorOffset);
        Assert.Equal(((short)center.X, (short)center.Y), DirectBackgroundScrollCertificate.ResolveNormalCenter(field, following, actors));
        SceneEventState restored = JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(following), options)!;
        Assert.True(ActorBackgroundScrollCertificate.CanRestore(field, restored, actors));
        Assert.Equal(selected.ActorOffset, NormalCameraCertificate.Prepare(field, restored, actors)!.ActorOffset);
    }

    [Fact]
    public void ActualSelectedCourtCosineOwnsRowOneThroughCompletionAndTheLaterCameraZeroSwitch()
    {
        string source = TestResources.FixtureFile("../fixtures/external-fixture.json");
        Assert.SkipUnless(File.Exists(source), "The genuine court selected cosine boundary snapshot is required.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(source));
        JsonElement checkpoint = document.RootElement.GetProperty("EventState");
        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        SceneEventState scene = JsonSerializer.Deserialize<SceneEventState>(checkpoint.GetProperty("Scene"), options)!;
        ActorState[] actors = JsonSerializer.Deserialize<ActorState[]>(checkpoint.GetProperty("Actors"), options)!;
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldAssets field = assets.LoadField(879);
        Assert.Equal(1, scene.CameraIndex);
        Assert.Equal(((short)0, (short)-8), (scene.NormalBackgroundScrollCenter!.X, scene.NormalBackgroundScrollCenter.Y));
        Assert.True(DirectBackgroundScrollCertificate.CanSetCosine(field, scene, 1, 0, -50, 150));
        SceneEventState? terminal = null;
        foreach (short elapsed in new short[] { 0, 75, 150 })
        {
            short y = (short)NativeCosineInterpolation.Interpolate(-8, -50, 150, elapsed);
            BackgroundScrollPosition row = new(0, y, 0, -50, 0, -8, elapsed == 150 ? elapsed : (short)(elapsed + 1), 5, elapsed == 150 ? (byte)2 : (byte)1, 150);
            JsonNode phase = JsonSerializer.SerializeToNode(scene)!;
            phase.AsObject().Remove("NormalBackgroundScrollCenter");
            phase["BackgroundScrollPositions"]!["1"] = JsonSerializer.SerializeToNode(row);
            SceneEventState current = phase.Deserialize<SceneEventState>(options)!;
            Assert.True(ActorBackgroundScrollCertificate.CanRestore(field, current, actors));
            Assert.Null(ActorBackgroundScrollCertificate.Prepare(field, current));
            Assert.Equal(new Vector2(0, -y), DirectBackgroundScrollCertificate.Prepare(field, current)!.ActorOffset);
            Assert.Null(DirectBackgroundScrollCertificate.ResolveNormalCenter(field, current, actors));
            SceneEventState restored = JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(current), options)!;
            Assert.True(ActorBackgroundScrollCertificate.CanRestore(field, restored, actors));
            Assert.Equal(new Vector2(0, -y), DirectBackgroundScrollCertificate.Prepare(field, restored)!.ActorOffset);
            terminal = current;
        }
        JsonNode switched = JsonSerializer.SerializeToNode(terminal)!;
        switched["CameraIndex"] = 0;
        SceneEventState cameraZero = switched.Deserialize<SceneEventState>(options)!;
        (short X, short Y)? resolvedActor = ActorBackgroundScrollCertificate.Resolve(field, cameraZero, actors, 4);
        Assert.True(resolvedActor.HasValue, $"Camera0 baseline={DirectBackgroundScrollCertificate.HasSelectedBaseline(field, cameraZero)}, wrap={field.Information!.BackgroundWrapMask}, layers={string.Join(',', field.Background.Select(tile => tile.Layer).Distinct())}, row1={NativeNonselectedBackgroundScrollCertificate.SupportsRetainedPosition(field, cameraZero, 1, cameraZero.BackgroundScrollPositions[1])}.");
        (short X, short Y) selectedActor = resolvedActor.Value;
        switched["ActorBackgroundScroll"] = JsonSerializer.SerializeToNode(cameraZero.ActorBackgroundScroll! with { X = selectedActor.X, Y = selectedActor.Y });
        cameraZero = switched.Deserialize<SceneEventState>(options)!;
        Assert.True(ActorBackgroundScrollCertificate.CanRestore(field, cameraZero, actors));
        Assert.Equal(new Vector2(-selectedActor.X, -selectedActor.Y), ActorBackgroundScrollCertificate.Prepare(field, cameraZero)!.ActorOffset);
        Assert.Equal((byte)2, cameraZero.BackgroundScrollPositions[1].Status);
        Assert.Equal((short)-50, cameraZero.BackgroundScrollPositions[1].Y);
    }

    private static ActorState Actor(int objectId, int x, int y)
    {
        JsonNode state = JsonSerializer.SerializeToNode(new ActorState(objectId, objectId))!;
        state["ModelId"] = 0;
        state["X"] = x;
        state["Y"] = y;
        state["Z"] = 0;
        return state.Deserialize<ActorState>()!;
    }

    private static SceneEventState Scene(object properties)
    {
        JsonNode state = JsonSerializer.SerializeToNode(new SceneEventState())!;
        state["BackgroundScrollLayers"] = JsonSerializer.SerializeToNode(Enumerable.Range(0, 8).ToDictionary(layer => layer, _ => new BackgroundScrollSettings(0, 0, 256, 256)));
        foreach ((string name, JsonNode? value) in JsonSerializer.SerializeToNode(properties)!.AsObject())
        {
            state[name] = value?.DeepClone();
        }
        return state.Deserialize<SceneEventState>()!;
    }
}
