using System.Numerics;
using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal static partial class CatalogPreviewPresentation
{
    public static ModPreviewResourcesProfile CreateProfile(string directory, string? models, string? language)
    {
        return new(directory, models, language)
        {
            RenderField = RenderField,
            RenderBattleStage = RenderBattleStage,
            RenderFieldPatch = RenderFieldPatch
        };
    }

    public static PixelImage RenderField(FieldAssets field) => RenderFieldPatch(field, []);

    private static PixelImage RenderBattleStage(BattleStage stage)
    {
        BattleRenderer renderer = new(640, 480);
        BattleCamera camera = stage.IntroductionCameras.TryGetValue(0, out BattleStageCamera? introduction) ? BattleCamera.FromNative(introduction) : BattleCamera.NativeInitial;
        byte[] rgba = renderer.Render(stage, [], camera);
        return new(renderer.Width, renderer.Height, MemoryMarshal.Cast<byte, Rgba32>(rgba).ToArray());
    }

    private static PixelImage RenderFieldPatch(FieldAssets field, IReadOnlyList<FieldBackgroundPatch> patches)
    {
        FieldRenderer renderer = new(field, FieldRenderer.Width * 2, FieldRenderer.Height * 2);
        Dictionary<int, int> states = field.Background.Where(tile => tile.AnimationId != 255).GroupBy(tile => tile.AnimationId).ToDictionary(group => group.Key, group => (int)group.First().AnimationState);
        Dictionary<BackgroundTile, bool> visibility = [];
        foreach (FieldBackgroundPatch patch in patches)
        {
            foreach (BackgroundTile tile in field.Background.Where(patch.Matches))
            {
                visibility.TryAdd(tile, patch.Visible);
            }
        }
        byte[] rgba = renderer.Render(states, [], TimeSpan.Zero, backgroundTileVisibility: visibility);
        return new(renderer.OutputWidth, renderer.OutputHeight, MemoryMarshal.Cast<byte, Rgba32>(rgba).ToArray());
    }

    public static async Task<ModPreviewComparison> CompareAsync(ModPreviewDocument document, ModPreviewEntry entry, ModPreviewResourcesProfile profile, CancellationToken token)
    {
        ModPreviewComparison comparison = await document.CompareAsync(entry, profile, token).ConfigureAwait(false);
        return await Task.Run(() => DrawModels(comparison, 0.4f, 0), token).ConfigureAwait(false);
    }

    internal sealed class WorldComparison(ModPreviewResourcesProfile profile, ModCatalogSnapshot catalog) : IDisposable
    {
        private readonly object gate = new();
        private WorldMapModPreview? world;
        private WorldMapModPreviewResult? frame;
        private bool disposed;

        public Task<ModPreviewComparison> RenderAsync(float east, float south, CancellationToken token)
        {
            return RenderAsync(new(east, south), Vector3.Zero, token);
        }

        public Task<ModPreviewComparison> FlyAsync(Vector3 movement, CancellationToken token)
        {
            return RenderAsync(Vector2.Zero, movement, token);
        }

        private Task<ModPreviewComparison> RenderAsync(Vector2 mapStep, Vector3 flightStep, CancellationToken token)
        {
            return Task.Run(() =>
            {
                lock (gate)
                {
                    ObjectDisposedException.ThrowIf(disposed, this);
                    token.ThrowIfCancellationRequested();
                    world ??= WorldMapModPreview.Open(profile, catalog, token);
                    WorldMapModPreviewView view = new();
                    if (frame is { } current)
                    {
                        WorldCamera camera = current.Camera;
                        Vector3 offset = new(mapStep.X, 0, mapStep.Y);
                        if (flightStep != Vector3.Zero)
                        {
                            Vector3 forward = camera.FootFrame(current.Position).Forward with { Y = 0 };
                            forward = Vector3.Normalize(forward);
                            Vector3 right = Vector3.Cross(forward, Vector3.UnitY);
                            offset += right * flightStep.X + Vector3.UnitY * flightStep.Y + forward * flightStep.Z;
                        }
                        float x = WorldAssets.Wrap(current.Position.X + offset.X, WorldAssets.Columns * WorldAssets.SegmentUnits);
                        float z = WorldAssets.Wrap(current.Position.Z + offset.Z, WorldAssets.Rows * WorldAssets.SegmentUnits);
                        if (flightStep != Vector3.Zero || camera.TargetPosition.HasValue)
                        {
                            float height = camera.TargetPosition?.Y ?? current.Position.Y + camera.TargetHeight * 16;
                            camera = camera with { TargetPosition = new Vector3(x, height + offset.Y, z) };
                        }
                        view = new(new Vector2(x, z), camera) { UseRegionalSky = true };
                    }
                    frame = world.Render(view, token);
                    return new ModPreviewComparison(new("World map", frame.Original), new("World map", frame.Modded));
                }
            }, token);
        }

        public void Dispose()
        {
            // Navigation closes immediately, even if the current frame is still being decoded.
            _ = Task.Run(() =>
            {
                lock (gate)
                {
                    disposed = true;
                    world?.Dispose();
                    world = null;
                    frame = null;
                }
            });
        }
    }

    public static ModPreviewComparison DrawModels(ModPreviewComparison comparison, float yaw, float seconds)
    {
        ModelPreviewBounds? framing = comparison is { Before.Model: { } before, After.Model: { } after }
            ? ModelPreviewRenderer.Measure(before).Union(ModelPreviewRenderer.Measure(after))
            : null;
        return comparison with { Before = DrawSide(comparison.Before), After = DrawSide(comparison.After) };

        ModPreviewSide DrawSide(ModPreviewSide side)
        {
            if (side.Model is not { } model)
            {
                return side;
            }
            PreviewRaster raster = new();
            ModelPreviewRenderer.Render(raster, model, 480, 360, yaw, 0.13f, 1, seconds, framing: framing);
            return side with { Image = raster.Image };
        }
    }

    private sealed class PreviewRaster : ITriangleRenderer
    {
        private float[] depth = [];
        public PixelImage Image { get; private set; } = null!;

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Image = new(width, height);
            Array.Fill(Image.Pixels, Pixel(clearColor));
            depth = new float[Image.Pixels.Length];
            Array.Fill(depth, clearDepth);
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vector2 ab = b.Position - a.Position;
            Vector2 ac = c.Position - a.Position;
            float area = Cross(ab, ac);
            if (MathF.Abs(area) < 0.001f)
            {
                return;
            }
            Vector2 min = Vector2.Min(a.Position, Vector2.Min(b.Position, c.Position));
            Vector2 max = Vector2.Max(a.Position, Vector2.Max(b.Position, c.Position));
            int left = Math.Clamp((int)MathF.Floor(min.X), 0, Image.Width - 1);
            int top = Math.Clamp((int)MathF.Floor(min.Y), 0, Image.Height - 1);
            int right = Math.Clamp((int)MathF.Ceiling(max.X), 0, Image.Width - 1);
            int bottom = Math.Clamp((int)MathF.Ceiling(max.Y), 0, Image.Height - 1);
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    Vector2 point = new(x + 0.5f - a.Position.X, y + 0.5f - a.Position.Y);
                    float bWeight = Cross(point, ac) / area;
                    float cWeight = Cross(ab, point) / area;
                    float aWeight = 1 - bWeight - cWeight;
                    if (aWeight < 0 || bWeight < 0 || cWeight < 0)
                    {
                        continue;
                    }
                    int index = y * Image.Width + x;
                    float z = a.Depth * aWeight + b.Depth * bWeight + c.Depth * cWeight;
                    if (z > depth[index])
                    {
                        continue;
                    }
                    Vector4 color = a.Color * aWeight + b.Color * bWeight + c.Color * cWeight;
                    if (material.Texture is { } texture)
                    {
                        Vector2 uv = a.Uv * aWeight + b.Uv * bWeight + c.Uv * cWeight;
                        int tx = Math.Min(texture.Width - 1, (int)((uv.X - MathF.Floor(uv.X)) * texture.Width));
                        int ty = Math.Min(texture.Height - 1, (int)((uv.Y - MathF.Floor(uv.Y)) * texture.Height));
                        color *= Vector(texture[tx, ty]);
                    }
                    if (color.W < material.MinimumAlpha)
                    {
                        continue;
                    }
                    if (material.Blend == TriangleBlend.Alpha)
                    {
                        color = Vector4.Lerp(Vector(Image.Pixels[index]), color, color.W) with { W = 1 };
                    }
                    Image.Pixels[index] = Pixel(color);
                    depth[index] = z;
                }
            }
        }

        public void End()
        {
        }

        private static float Cross(Vector2 left, Vector2 right) => left.X * right.Y - left.Y * right.X;
        private static Vector4 Vector(Rgba32 color) => new Vector4(color.R, color.G, color.B, color.A) / 255f;
        private static Rgba32 Pixel(Vector4 color)
        {
            color = Vector4.Clamp(color, Vector4.Zero, Vector4.One) * 255;
            return new((byte)color.X, (byte)color.Y, (byte)color.Z, (byte)color.W);
        }
    }
}
