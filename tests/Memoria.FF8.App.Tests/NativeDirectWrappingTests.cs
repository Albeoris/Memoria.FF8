using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeDirectWrappingTests
{
    [Fact]
    public void SharedDirectCenterUsesOriginalWrappingPeriodsAndMatchesIndependentPlacementPixels()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field resources are required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(251);
        SceneEventState scene = CreateScene(field);
        Assert.True(DirectBackgroundScrollCertificate.CanSet(field, scene, 0, 0, 8));
        Assert.True(DirectBackgroundScrollCertificate.CanSet(field, scene, 0, -32, -8));
        Assert.False(DirectBackgroundScrollCertificate.CanSet(field, scene, 0, 0, 9));
        Assert.False(DirectBackgroundScrollCertificate.CanSet(field, scene, 1, 0, 8));
        FieldAssets anotherIdentity = field with { Id = 600 };
        Assert.True(DirectBackgroundScrollCertificate.CanSet(anotherIdentity, CreateScene(anotherIdentity), 0, 0, 8));
        foreach (short y in new short[] { -8, 0, 8 })
        {
            scene.BackgroundScrollPositions[0] = new BackgroundScrollPosition(0, y, 0, y, 13, -7, 0, 3, 2);
            BackgroundScrollFrame current = DirectBackgroundScrollCertificate.Prepare(field, scene)!;
            Assert.Equal(new Vector2(0, -y), current.ActorOffset);
            FieldNativeBackgroundWrapFrame wrap = Assert.IsType<FieldNativeBackgroundWrapFrame>(current.NativeWrap);
            Assert.Equal((384, 240), ((int)Assert.Single(wrap.Layers).PeriodX, (int)Assert.Single(wrap.Layers).PeriodY));
            List<BackgroundTile> placed = [];
            foreach (BackgroundTile tile in field.Background)
            {
                int x = unchecked((short)tile.X);
                int yy = unchecked((short)tile.Y);
                if (tile.Layer == 1)
                {
                    x = wrap.Left > x ? unchecked((short)(x + 384)) : wrap.Right < x ? unchecked((short)(x - 384)) : x;
                    yy = wrap.Top > yy ? unchecked((short)(yy + 240)) : wrap.Bottom < yy ? unchecked((short)(yy - 240)) : yy;
                    if (wrap.Left >= x || wrap.Right <= x || wrap.Top >= yy || wrap.Bottom <= yy)
                    {
                        continue;
                    }
                }
                placed.Add(tile with { X = x, Y = yy - y });
            }
            FieldRenderer renderer = new(field, 320, 240);
            FieldRenderer reference = new(field with { Background = placed }, 320, 240);
            Assert.Equal(reference.Render(new Dictionary<int, int>(), [], TimeSpan.Zero), renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: current.LayerOffsets, actorScrollOffset: current.ActorOffset, nativeWrapFrame: wrap));
        }
        scene.BackgroundScrollPositions[0] = new BackgroundScrollPosition(0, 9, 0, 9, 0, 0, 0, 3, 2);
        Assert.False(DirectBackgroundScrollCertificate.CanSetCosine(field, scene, 0, 0, 8, 30));
        scene.BackgroundScrollLayers[1] = new BackgroundScrollSettings(0, 0, 128, 256);
        Assert.True(DirectBackgroundScrollCertificate.CanSet(field, scene, 0, 0, 8));
        int minimumCenterX = field.Information!.CameraBounds[0].Left + 160;
        Assert.NotInRange(minimumCenterX + short.MaxValue - minimumCenterX * 128 / 256, -4096, 4095);
        scene.BackgroundScrollLayers[1] = new BackgroundScrollSettings(short.MaxValue, 0, 128, 256);
        Assert.False(DirectBackgroundScrollCertificate.CanSet(field, scene, 0, 0, 8));
    }

    [Fact]
    public void WrappedNormalCameraProducerPublishesItsActualProjectionWithoutAFieldAdmissionList()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field resources are required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(251);
        SceneEventState scene = CreateScene(field);
        ActorState actor = CreatePartyActor();
        scene.NativeActorObjectIds.Add(1);
        scene.FieldPartyNativeActorIds.AddRange([0, 255, 255]);
        BackgroundScrollFrame normal = NormalCameraCertificate.Prepare(field, scene, [actor])!;
        Assert.Equal(((short)-normal.ActorOffset.X, (short)-normal.ActorOffset.Y), DirectBackgroundScrollCertificate.ResolveNormalCenter(field, scene, [actor]));
        scene.BackgroundScrollPositions[0] = new BackgroundScrollPosition(0, 8, 0, 8, 0, 0, 0, 3, 2);
        Assert.Null(DirectBackgroundScrollCertificate.ResolveNormalCenter(field, scene, [actor]));
    }

    private static ActorState CreatePartyActor()
    {
        System.Text.Json.Nodes.JsonObject actor = JsonSerializer.SerializeToNode(new ActorState(1, 0))!.AsObject();
        actor[nameof(ActorState.ModelId)] = 1;
        actor[nameof(ActorState.CharacterId)] = 0;
        actor[nameof(ActorState.X)] = 459;
        actor[nameof(ActorState.Y)] = 1659;
        actor[nameof(ActorState.Z)] = 0;
        return actor.Deserialize<ActorState>()!;
    }

    private static SceneEventState CreateScene(FieldAssets field)
    {
        NativeFieldInitializationProof proof = new(1, NativeFieldRuntimeProfile.FF8EnExecutableHash, field.Id, Convert.ToHexString(SHA256.HashData(field.Jsm)), 20, NativeFieldInitializationStage.Completed);
        return JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(new { NativeMovieMask = (NativeMovieMaskState?)null, NativeLayerZeroScrollCurrent = (NativeLayerZeroScrollCurrent?)null, NativeLadderSpeed490E = (byte?)null, NativeDrawPointDisplayMask = (ushort?)null, DrawPointInteraction = (NativeDrawPointInteraction?)null, ModelParts = (NativeFieldModelPartsState?)null, AudioLoadEpoch = 1, NativeSalaryMovementUnits = (byte?)null, BackgroundScrollLayers = Enumerable.Range(0, 8).ToDictionary(layer => layer, _ => new BackgroundScrollSettings(0, 0, 256, 256)), NativeFieldInitialization = proof, NativeFieldLoadVersion = 1, NativeActorLayoutVersion = 1, FieldCallbackClock = new NativeFieldCallbackClock(1, 1, 0, 0, 0), PartyBindingMemory = new NativePartyBindingMemory(1, 0, 255, 255), Party = new[] { 0, 255, 255 } }))!;
    }

    [Fact]
    public void SavedPartyActorProjectionUsesTheSameWrappedFrameThroughoutCosineAndContinuousModes()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field resources are required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(251);
        SceneEventState scene = CreateScene(field);
        scene.NativeActorObjectIds.Add(1);
        scene.FieldPartyNativeActorIds.AddRange([0, 255, 255]);
        ActorState actor = CreatePartyActor();
        scene.BackgroundScrollPositions[0] = new BackgroundScrollPosition(0, 8, 0, 8, -32, 8, 0, 3, 2);
        Assert.True(ActorBackgroundScrollCertificate.CanSetInterpolated(field, scene, [actor], 0, 15));
        Assert.True(BackgroundScrollCertificate.CanSetPartyPosition(field, scene, [actor], 0, 0, 15));
        Assert.False(BackgroundScrollCertificate.CanSetPartyPosition(field, scene, [actor], 1, 0, 15));
        Assert.False(ActorBackgroundScrollCertificate.CanSetInterpolated(field, scene, [actor], 1, 15));
        (short X, short Y) target = ActorBackgroundScrollCertificate.Resolve(field, scene, [actor], 0)!.Value;
        Assert.Equal(((short)32, (short)8), target);
        for (short elapsed = 0; elapsed <= 15; elapsed++)
        {
            short x = (short)NativeCosineInterpolation.Interpolate(0, target.X, 15, elapsed);
            short y = (short)NativeCosineInterpolation.Interpolate(8, target.Y, 15, elapsed);
            NativeActorBackgroundScroll state = new(1, 1, 1, 0, elapsed == 15 ? (byte)2 : (byte)1, x, y, 0, 8, 0, 2, 15, elapsed == 15 ? elapsed : (short)(elapsed + 1), target.X, target.Y);
            var json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(scene))!;
            json["BackgroundScrollPositions"] = new System.Text.Json.Nodes.JsonObject();
            json["ActorBackgroundScroll"] = JsonSerializer.SerializeToNode(state);
            SceneEventState current = JsonSerializer.Deserialize<SceneEventState>(json.ToJsonString())!;
            Assert.True(ActorBackgroundScrollCertificate.CanRestore(field, current, [actor]));
            BackgroundScrollFrame prepared = ActorBackgroundScrollCertificate.Prepare(field, current)!;
            BackgroundScrollFrame shared = DirectBackgroundScrollCertificate.PrepareCenter(field, current, x, y);
            Assert.Equal(shared.ActorOffset, prepared.ActorOffset);
            Assert.True(prepared.NativeWrap!.Layers.SequenceEqual(shared.NativeWrap!.Layers));
            FieldRenderer renderer = new(field, 320, 240);
            Assert.NotEmpty(renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: prepared.LayerOffsets, actorScrollOffset: prepared.ActorOffset, nativeWrapFrame: prepared.NativeWrap));
        }
        NativeActorBackgroundScroll continuous = new(1, 1, 1, 0, 1, target.X, target.Y, 0, 8, 0);
        var continuousJson = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(scene))!;
        continuousJson["BackgroundScrollPositions"] = new System.Text.Json.Nodes.JsonObject();
        continuousJson["ActorBackgroundScroll"] = JsonSerializer.SerializeToNode(continuous);
        Assert.True(ActorBackgroundScrollCertificate.CanRestore(field, JsonSerializer.Deserialize<SceneEventState>(continuousJson.ToJsonString())!, [actor]));
    }
}
