using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWrappedShakeRenderTests
{
    [Fact]
    public void ActualTrainWrappingKeepsItsWindowThenAddsTheSignedDrawingOriginOnce()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(902);
        Dictionary<int, BackgroundScrollSettings> settings = Enumerable.Range(0, 8).ToDictionary(layer => layer, _ => new BackgroundScrollSettings(0, 0, 256, 256));
        settings[1] = new BackgroundScrollSettings(0, 0, 0, 0);
        NativeFieldInitializationProof proof = new(1, NativeFieldRuntimeProfile.FF8EnExecutableHash, field.Id, Convert.ToHexString(SHA256.HashData(field.Jsm)), 20, NativeFieldInitializationStage.Completed);
        // Declared current component words isolate the reached geometry and draw-origin composition, without a seeded checkpoint.
        SceneEventState scene = JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(new { NativeMovieMask = (NativeMovieMaskState?)null, NativeLayerZeroScrollCurrent = (NativeLayerZeroScrollCurrent?)null, NativeLadderSpeed490E = (byte?)null, NativeDrawPointDisplayMask = (ushort?)null, DrawPointInteraction = (NativeDrawPointInteraction?)null, ModelParts = (NativeFieldModelPartsState?)null, AudioLoadEpoch = 1, NativeSalaryMovementUnits = (byte?)null, BackgroundScrollLayers = settings, BackgroundScrollPositions = new Dictionary<int, BackgroundScrollPosition> { [0] = new(160, -8, 160, -8, 90, -8, 30, 4, 2, 30, NativeLinearVersion: 1), [1] = new(-64, 0, -320, 0, 0, 0, 3, 4, 1, 10, NativeLinearVersion: 1) }, NativeFieldInitialization = proof, NativeFieldLoadVersion = 1, NativeActorLayoutVersion = 1, Party = new[] { 0, 255, 255 } }))!;
        BackgroundScrollFrame prepared = DirectBackgroundScrollCertificate.Prepare(field, scene)!;
        FieldNativeBackgroundWrapFrame frame = prepared.NativeWrap!;
        // Compare the native drawing rectangle; widescreen strips have their own placement contract.
        FieldRenderer renderer = new(field, 640, 448, nativeMovieViewport: true);
        byte[] baseline = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: prepared.LayerOffsets, actorScrollOffset: prepared.ActorOffset, nativeWrapFrame: frame).ToArray();
        foreach ((sbyte jitterX, sbyte jitterY) in new (sbyte, sbyte)[] { (0, 1), (7, -3), (-4, 2) })
        {
            List<BackgroundTile> placed = [];
            foreach (BackgroundTile tile in field.Background)
            {
                int x = unchecked((short)(tile.X + (tile.Layer == 1 ? 224 : 0)));
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
                placed.Add(tile with { X = x - 160 + jitterX, Y = y + 8 + jitterY });
            }
            FieldRenderer independent = new(field with { Background = placed }, 640, 448, nativeMovieViewport: true);
            byte[] actual = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: prepared.LayerOffsets, actorScrollOffset: prepared.ActorOffset, nativeWrapFrame: frame, drawingJitterX: jitterX, drawingJitterY: jitterY).ToArray();
            Assert.Equal(independent.Render(new Dictionary<int, int>(), [], TimeSpan.Zero), actual);
            Assert.False(baseline.SequenceEqual(actual));
            Assert.Equal(actual, renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: prepared.LayerOffsets, actorScrollOffset: prepared.ActorOffset, nativeWrapFrame: frame, drawingJitterX: jitterX, drawingJitterY: jitterY));
        }
        Assert.Equal(baseline, renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: prepared.LayerOffsets, actorScrollOffset: prepared.ActorOffset, nativeWrapFrame: frame));
    }

    [Fact]
    public void ModelsReceiveTheSameDrawingShiftAndUnprovedE5LeavesPixelsUntouched()
    {
        PixelImage texture = new(1, 1, [new Rgba32(100, 140, 200, 255)]);
        // The model root rotates Y by a quarter turn before the actor's field direction is applied.
        FieldModel model = new("drawing-origin-component", [new ModelBone(-1, 0)], [new ModelVertex(new Vector3(0, -16, -16), 0), new ModelVertex(new Vector3(0, -16, 16), 0), new ModelVertex(new Vector3(0, 16, 0), 0)], [new ModelFace([0, 1, 2], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, false)], [texture], [new ModelAnimation([new AnimationFrame(Vector3.Zero, [Vector3.Zero])])]);
        FieldAssets field = new(600, "drawing-origin-component", [], [], [], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 256), Vector2.Zero, 256), new FieldWalkmesh([]), [model]);
        FieldRenderer renderer = new(field, modelTextureSampling: ModelTextureSampling.Nearest);
        ActorVisual actor = new(0, 0, 0, 0, 0, 0, 0, 0, true, PolygonColor: new Vector3(128));
        byte[] baseline = renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero).ToArray();
        Assert.Contains((byte)200, baseline);
        FieldRenderer reference = new(field, modelTextureSampling: ModelTextureSampling.Nearest);
        byte[] shifted = renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero, drawingJitterX: 7, drawingJitterY: -3);
        Assert.Equal(reference.Render(new Dictionary<int, int>(), [actor with { X = 7, Y = -3 }], TimeSpan.Zero), shifted);
        byte[] retained = shifted.ToArray();
        Assert.Throws<NotSupportedException>(() => renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero, actorScrollOffset: new Vector2(1000, 0), drawingJitterX: 127));
        Assert.Equal(retained, shifted);
        Assert.Equal(baseline, renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero));
    }
}
