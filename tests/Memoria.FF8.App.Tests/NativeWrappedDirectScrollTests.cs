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
    public void ActualRoofDirectLayerUsesTheSharedCurrentPlacementAndKeepsUnwrappedOrigins()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
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
        List<BackgroundTile> placed = [];
        foreach (BackgroundTile tile in field.Background)
        {
            BackgroundScrollSettings settings = scene.BackgroundScrollLayers[tile.Layer];
            int translationX = tile.Layer == 0 ? 0 : center.X + settings.OffsetX - center.X * settings.ScaleX / 256 - (tile.Layer == 1 ? 240 : 0);
            int translationY = tile.Layer == 0 ? 0 : center.Y + settings.OffsetY - center.Y * settings.ScaleY / 256;
            int x = unchecked((short)(tile.X + translationX));
            int y = unchecked((short)(tile.Y + translationY));
            if ((field.Information.BackgroundWrapMask & (1 << tile.Layer)) != 0)
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
        Dictionary<int, Vector2> offsets = field.Background.Select(tile => tile.Layer).Distinct().ToDictionary(layer => layer, _ => new Vector2(-center.X, -center.Y));
        FieldRenderer renderer = new(field, 640, 448);
        FieldRenderer independent = new(field with { Background = placed }, 640, 448);
        Assert.Equal(independent.Render(new Dictionary<int, int>(), [], TimeSpan.Zero), renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: new Vector2(-center.X, -center.Y), nativeWrapFrame: frame));
    }

    private static SceneEventState ComponentScene(FieldAssets field)
    {
        NativeFieldInitializationProof proof = new(1, NativeFieldRuntimeProfile.FF8EnExecutableHash, field.Id, Convert.ToHexString(SHA256.HashData(field.Jsm)), 20, NativeFieldInitializationStage.Completed);
        return JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(new { ModelParts = (NativeFieldModelPartsState?)null, AudioLoadEpoch = 1, NativeSalaryMovementUnits = (byte?)null, BackgroundScrollLayers = Enumerable.Range(0, 8).ToDictionary(layer => layer, _ => new BackgroundScrollSettings(0, 0, 256, 256)), NativeFieldInitialization = proof, NativeFieldLoadVersion = 1, NativeActorLayoutVersion = 1, Party = new[] { 0, 255, 255 } }))!;
    }
}
