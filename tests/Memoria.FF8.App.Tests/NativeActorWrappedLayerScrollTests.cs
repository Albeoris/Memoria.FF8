using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeActorWrappedLayerScrollTests
{
    [Fact]
    public void ExactRetainedActorProjectionSurvivesConcurrentWrappedLinearLayer()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(931);
        NativeFieldInitializationProof proof = new(1, NativeFieldRuntimeProfile.FF8EnExecutableHash, field.Id, Convert.ToHexString(SHA256.HashData(field.Jsm)), 20, NativeFieldInitializationStage.Completed);
        Dictionary<int, BackgroundScrollSettings> settings = Enumerable.Range(0, 8).ToDictionary(layer => layer, _ => new BackgroundScrollSettings(0, 0, 256, 256));
        settings[1] = new BackgroundScrollSettings(0, 0, 0, 0);
        NativeActorBackgroundScroll camera = new(1, 3, 3, 3, 0, 160, -8, null, null, Mode: 1, Duration: 60);
        // Explicit component words reproduce the reached camera/geometry contract, without importing its session history.
        SceneEventState scene = Component<SceneEventState>(new SceneEventState(), new { NativeMovieMask = (NativeMovieMaskState?)null, NativeLayerZeroScrollCurrent = (NativeLayerZeroScrollCurrent?)null, NativeLadderSpeed490E = (short?)null, ModelParts = (NativeFieldModelPartsState?)null, AudioLoadEpoch = 1, NativeSalaryMovementUnits = (byte?)null, BackgroundScrollLayers = settings, BackgroundScrollPositions = new Dictionary<int, BackgroundScrollPosition> { [1] = new(0, 0, -320, 0, null, null, null, 4, 0, 10, NativeLinearVersion: 1) }, ActorBackgroundScroll = camera, NativeFieldInitialization = proof, NativeFieldLoadVersion = 1, NativeActorLayoutVersion = 1, NativeActorObjectIds = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 21 }, FieldPartyNativeActorIds = new[] { 0, 255, 255 }, Party = new[] { 0, 255, 255 }, FieldCallbackClock = new NativeFieldCallbackClock(1, 2, 0, 0, 0) });
        ActorState body = Body(active: true, model: 3);
        Assert.Equal(((short)-160, (short)-8), ActorBackgroundScrollCertificate.Resolve(field, scene, [body], 3));
        Assert.True(ActorBackgroundScrollCertificate.CanSetInterpolated(field, scene, [body], 3, 60));
        Assert.True(ActorBackgroundScrollCertificate.CanRestore(field, scene, [body]));
        Assert.Equal(((short)-160, (short)-8), ActorBackgroundScrollCertificate.Resolve(field, scene, [Body(active: false, model: -1)], 3));
        Assert.Null(ActorBackgroundScrollCertificate.Resolve(field, scene, [], 3));
        Assert.Null(ActorBackgroundScrollCertificate.Resolve(field, scene, [body], 255));

        BackgroundScrollFrame prepared = ActorBackgroundScrollCertificate.Prepare(field, scene)!;
        Assert.Equal(new Vector2(-160, 8), prepared.ActorOffset);
        FieldNativeBackgroundWrapLayer layer = Assert.Single(prepared.NativeWrap!.Layers);
        Assert.Equal((1, 160, -8), ((int)layer.Layer, (int)layer.OffsetX, (int)layer.OffsetY));
        List<BackgroundTile> placed = [];
        foreach (BackgroundTile tile in field.Background)
        {
            int x = unchecked((short)(tile.X + (tile.Layer == 1 ? 160 : 0)));
            int y = unchecked((short)(tile.Y + (tile.Layer == 1 ? -8 : 0)));
            if ((field.Information!.BackgroundWrapMask & (1 << tile.Layer)) != 0)
            {
                FieldBounds bounds = field.Information.CameraBounds[tile.Layer];
                x = x < -32 ? unchecked((short)(x + 2 * bounds.Right)) : x > 336 ? unchecked((short)(x - 2 * bounds.Right)) : x;
                y = y < -152 ? unchecked((short)(y + 2 * bounds.Bottom)) : y > 120 ? unchecked((short)(y - 2 * bounds.Bottom)) : y;
                if (x <= -32 || x >= 336 || y <= -152 || y >= 120)
                {
                    continue;
                }
            }
            placed.Add(tile with { X = x - 160, Y = y + 8 });
        }
        //Compare native DRAWENV placement; widescreen image-edge clamping changes when the reference tile geometry is translated.
        FieldRenderer current = new(field, 640, 448, nativeMovieViewport: true);
        FieldRenderer reference = new(field with { Background = placed }, 640, 448, nativeMovieViewport: true);
        Assert.Equal(reference.Render(new Dictionary<int, int>(), [], TimeSpan.Zero), current.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: prepared.LayerOffsets, actorScrollOffset: prepared.ActorOffset, nativeWrapFrame: prepared.NativeWrap));
        scene.BackgroundScrollLayers.Remove(1);
        Assert.Null(ActorBackgroundScrollCertificate.Resolve(field, scene, [body], 3));
    }

    private static T Component<T>(T allocation, object words)
    {
        JsonObject state = JsonSerializer.SerializeToNode(allocation)!.AsObject();
        foreach (KeyValuePair<string, JsonNode?> word in JsonSerializer.SerializeToNode(words)!.AsObject())
        {
            state[word.Key] = word.Value?.DeepClone();
        }
        return state.Deserialize<T>()!;
    }

    private static ActorState Body(bool active, int model)
    {
        return Component<ActorState>(new ActorState(3, 15), new { ObjectId = 3, Label = 15, ModelId = model, Active = active, X = 44, Y = -2068, Z = 895, NativeLadderAnimation = (NativeLadderAnimationState?)null, NativeMovementAccelerationTarget = (short?)null, NativeMovementCompletion542 = (ushort?)null, NativeLandingTriangle508 = (ushort?)null, NativeTurnAccumulator474 = (short?)null, PolygonColor = (Vector3?)null });
    }
}
