using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWrappedDirectScrollTests
{
    [Fact]
    public void SelectedLinearCenterUsesExistingModeFourAndIndependentWrappedPixels()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(930);
        SceneEventState scene = ComponentScene(field);
        // Declared component records match the reached physical phase words, without loading/patching its checkpoint.
        scene.BackgroundScrollLayers[2] = new BackgroundScrollSettings(0, 0, 0, 0);
        scene.BackgroundScrollPositions[1] = new BackgroundScrollPosition(100, 0, 0, 0, 240, 0, 71, 4, 1, 120, NativeLinearVersion: 1);
        scene.BackgroundScrollPositions[2] = new BackgroundScrollPosition(-288, 0, -320, 0, 0, 0, 10, 4, 1, 10, NativeLinearVersion: 1);
        NativeLinearScrollQuery query = new(0, 55, -8, -15, -8, 60);
        Assert.True(DirectBackgroundScrollCertificate.CanSetLinear(field, query));
        Assert.True(DirectBackgroundScrollCertificate.CanBeginLinear(field, query, scene));
        Assert.False(DirectBackgroundScrollCertificate.CanSetLinear(field, query with { TargetY = 9 }));
        Assert.False(DirectBackgroundScrollCertificate.CanSetLinear(field, query with { Duration = -1 }));
        FieldRenderer renderer = new(field, 640, 448);
        foreach (short elapsed in new short[] { 0, 1, 29, 30, 59, 60 })
        {
            short x = (short)(55 + elapsed * (-15 - 55) / 60);
            scene.BackgroundScrollPositions[0] = new BackgroundScrollPosition(x, -8, -15, -8, 55, -8, elapsed == 60 ? elapsed : (short)(elapsed + 1), 4, elapsed == 60 ? (byte)2 : (byte)1, 60, NativeLinearVersion: 1);
            Assert.True(NativeLinearScrollPhase.IsValid(scene.BackgroundScrollPositions[0]));
            Assert.True(DirectBackgroundScrollCertificate.SupportsLinearPhase(field, scene));
            BackgroundScrollFrame prepared = DirectBackgroundScrollCertificate.Prepare(field, scene)!;
            Assert.Equal(new Vector2(-x, 8), prepared.ActorOffset);
            Assert.Equal(IndependentPixels(field, scene, new FieldNativeCameraCenter(x, -8)), renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: prepared.LayerOffsets, actorScrollOffset: prepared.ActorOffset, nativeWrapFrame: prepared.NativeWrap));
        }
        scene.BackgroundScrollPositions[0] = new BackgroundScrollPosition(55, -8, -15, -8, 55, -8, 0, 4, 2, 0, NativeLinearVersion: 1);
        Assert.True(DirectBackgroundScrollCertificate.SupportsLinearPhase(field, scene));
        Assert.Equal((short)55, NativeLinearScrollPhase.Interpolate(55, -15, 0, 0));
        scene.BackgroundScrollLayers.Remove(2);
        Assert.False(DirectBackgroundScrollCertificate.CanBeginLinear(field, query, scene));
    }

    [Fact]
    public void ActualRoofDirectLayerUsesTheSharedCurrentPlacementAndKeepsUnwrappedOrigins()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(930);
        Assert.Equal(4, field.Information!.BackgroundWrapMask);
        SceneEventState scene = ComponentScene(field);
        Assert.True(NativeWrappedBackgroundScrollCertificate.CanSetDirect(field, scene, 1, 240, 0));
        Assert.False(NativeWrappedBackgroundScrollCertificate.CanSetDirect(field, scene, 0, 240, 0));
        Assert.False(NativeWrappedBackgroundScrollCertificate.CanSetDirect(field, scene, 1, 4097, 0));
        // Declared component records isolate admission/placement; no game checkpoint is loaded or padded.
        scene.BackgroundScrollPositions[1] = new BackgroundScrollPosition(null, null, 240, 0, null, null, null, 3, 0);
        Assert.True(NativeWrappedBackgroundScrollCertificate.CanSetDirect(field, scene, 1, 144, 0));
        Assert.True(BackgroundScrollCertificate.CanSetRatioForState(field, scene, [], 2, new BackgroundScrollSettings(0, 0, 0, 0)));
        Assert.False(NativeWrappedBackgroundScrollCertificate.SupportsPhase(field, scene));
        scene.BackgroundScrollPositions[1] = new BackgroundScrollPosition(240, 0, 240, 0, null, null, 0, 3, 2);
        Assert.True(NativeWrappedBackgroundScrollCertificate.SupportsPhase(field, scene));
        Assert.True(NativeWrappedBackgroundScrollCertificate.CanBegin(field, new NativeLinearScrollQuery(1, 240, 0, 0, 0, 120), scene));
        FieldAssets anotherIdentity = field with { Id = 600 };
        Assert.True(NativeWrappedBackgroundScrollCertificate.CanSetDirect(anotherIdentity, ComponentScene(anotherIdentity), 1, 240, 0));
        Assert.True(NativeWrappedBackgroundScrollCertificate.CanSetSettings(field, scene, 2, new BackgroundScrollSettings(0, 0, -256, 512)));
        Assert.False(NativeWrappedBackgroundScrollCertificate.CanSetSettings(field, scene, 2, new BackgroundScrollSettings(0, 0, short.MaxValue, 0)));
        scene.BackgroundScrollLayers[2] = new BackgroundScrollSettings(0, 0, 0, 0);
        Assert.True(NativeWrappedBackgroundScrollCertificate.SupportsPhase(field, scene));
        Assert.True(NativeWrappedBackgroundScrollCertificate.CanBegin(field, new NativeLinearScrollQuery(2, 0, 0, -320, 0, 10), scene));
        FieldNativeCameraCenter center = new(160, -8);
        FieldNativeBackgroundWrapFrame frame = NativeWrappedBackgroundScrollCertificate.PrepareWrap(field, scene, center);
        FieldNativeBackgroundWrapLayer direct = frame.Layers.Single(layer => layer.Layer == 1);
        Assert.Equal(((short)-240, (short)0, (short)0, (short)0), (direct.OffsetX, direct.OffsetY, direct.PeriodX, direct.PeriodY));
        FieldNativeBackgroundWrapLayer zeroRatio = frame.Layers.Single(layer => layer.Layer == 2);
        Assert.Equal(((short)160, (short)-8), (zeroRatio.OffsetX, zeroRatio.OffsetY));
        Assert.True(DirectBackgroundScrollCertificate.CanSet(field, scene, 0, 160, -8));
        BackgroundScrollFrame selected = DirectBackgroundScrollCertificate.PrepareCenter(field, scene, 160, -8);
        Assert.Equal(frame.Layers, selected.NativeWrap!.Layers);
        Dictionary<int, Vector2> offsets = field.Background.Select(tile => tile.Layer).Distinct().ToDictionary(layer => layer, _ => new Vector2(-center.X, -center.Y));
        FieldRenderer renderer = new(field, 640, 448);
        Assert.Equal(IndependentPixels(field, scene, center), renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: new Vector2(-center.X, -center.Y), nativeWrapFrame: frame));
    }

    private static byte[] IndependentPixels(FieldAssets field, SceneEventState scene, FieldNativeCameraCenter center)
    {
        List<BackgroundTile> placed = [];
        foreach (BackgroundTile tile in field.Background)
        {
            BackgroundScrollSettings settings = scene.BackgroundScrollLayers[tile.Layer];
            BackgroundScrollPosition? current = scene.BackgroundScrollPositions.GetValueOrDefault(tile.Layer);
            int translationX = tile.Layer == 0 ? 0 : center.X + settings.OffsetX - center.X * settings.ScaleX / 256 - (current?.X ?? 0);
            int translationY = tile.Layer == 0 ? 0 : center.Y + settings.OffsetY - center.Y * settings.ScaleY / 256 - (current?.Y ?? 0);
            int x = unchecked((short)(tile.X + translationX));
            int y = unchecked((short)(tile.Y + translationY));
            if ((field.Information!.BackgroundWrapMask & (1 << tile.Layer)) != 0)
            {
                FieldBounds bounds = field.Information.CameraBounds[tile.Layer];
                x = x < center.X - 192 ? unchecked((short)(x + 2 * bounds.Right)) : x > center.X + 176 ? unchecked((short)(x - 2 * bounds.Right)) : x;
                y = y < center.Y - 144 ? unchecked((short)(y + 2 * bounds.Bottom)) : y > center.Y + 128 ? unchecked((short)(y - 2 * bounds.Bottom)) : y;
                if (x <= center.X - 192 || x >= center.X + 176 || y <= center.Y - 144 || y >= center.Y + 128)
                {
                    continue;
                }
            }
            placed.Add(tile with { X = x - center.X, Y = y - center.Y });
        }
        FieldRenderer independent = new(field with { Background = placed }, 640, 448);
        return independent.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
    }

    private static SceneEventState ComponentScene(FieldAssets field)
    {
        NativeFieldInitializationProof proof = new(1, NativeFieldRuntimeProfile.FF8EnExecutableHash, field.Id, Convert.ToHexString(SHA256.HashData(field.Jsm)), 20, NativeFieldInitializationStage.Completed);
        return JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(new { ModelParts = (NativeFieldModelPartsState?)null, AudioLoadEpoch = 1, NativeSalaryMovementUnits = (byte?)null, BackgroundScrollLayers = Enumerable.Range(0, 8).ToDictionary(layer => layer, _ => new BackgroundScrollSettings(0, 0, 256, 256)), NativeFieldInitialization = proof, NativeFieldLoadVersion = 1, NativeActorLayoutVersion = 1, Party = new[] { 0, 255, 255 } }))!;
    }
}
