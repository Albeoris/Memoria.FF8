using System.Numerics;
using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    public static Vector3[] MeasureModelPreview(CrossoverFf8BattleActorVisual visual)
    {
        List<Vector3> points = [];
        CrossoverFf8BattleActorRenderer.VisitGeometry(visual, (mesh, _, bones, _) => points.AddRange(CrossoverFf8BattleActorRenderer.WorldVertices(visual, mesh, bones)));
        return points.ToArray();
    }

    public const int ModelPreviewSampleScale = 2;

    public void RenderModelPreview(CrossoverFf8BattleActorVisual visual, BattleCamera camera, PixelImage destination)
    {
        if (TriangleRenderer is not null || Width != destination.Width * ModelPreviewSampleScale || Height != destination.Height * ModelPreviewSampleScale)
        {
            throw new InvalidOperationException("A model preview requires a software buffer with two samples per axis for each destination pixel.");
        }
        Begin(camera, transparentBackground: true);
        CrossoverFf8BattleActorRenderer.VisitGeometry(visual, (mesh, textures, bones, _) =>
        {
            Draw(mesh, CrossoverFf8BattleActorRenderer.WorldVertices(visual, mesh, bones), textures, 1);
        });
        ResolveModelPreview(destination);
    }

    private void ResolveModelPreview(PixelImage destination)
    {
        ReadOnlySpan<Rgba32> samples = MemoryMarshal.Cast<byte, Rgba32>(pixels);
        const int samplesPerPixel = ModelPreviewSampleScale * ModelPreviewSampleScale;
        for (int y = 0; y < destination.Height; y++)
        {
            for (int x = 0; x < destination.Width; x++)
            {
                int first = (y * Width + x) * ModelPreviewSampleScale;
                int covered = 0;
                int red = 0;
                int green = 0;
                int blue = 0;
                for (int row = 0; row < ModelPreviewSampleScale; row++)
                {
                    for (int column = 0; column < ModelPreviewSampleScale; column++)
                    {
                        int sample = first + row * Width + column;
                        // The software pass writes depth for every visible texel. Its palette alpha can carry the native semi-transparency bit even on opaque polygons.
                        if (depth[sample] <= 0)
                        {
                            continue;
                        }
                        Rgba32 color = samples[sample];
                        red += color.R;
                        green += color.G;
                        blue += color.B;
                        covered++;
                    }
                }
                int alpha = (covered * byte.MaxValue + samplesPerPixel / 2) / samplesPerPixel;
                destination[x, y] = covered == 0 ? default : new((byte)(red / covered), (byte)(green / covered), (byte)(blue / covered), (byte)alpha);
            }
        }
    }
}
