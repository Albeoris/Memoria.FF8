using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed record Ff7WorldRadarMarker(int Model, int X, int Z, bool Controlled = false);

public sealed class Ff7WorldEnvironmentAdapter
{
    private readonly Ff7WorldEnvironmentResources resources;
    private readonly Ff7WorldMath math;
    private readonly PixelImage radar;
    private readonly PixelImage map;
    private readonly PixelImage clouds;
    private readonly PixelImage largeMap;

    public Ff7WorldEnvironmentAdapter(Ff7WorldEnvironmentResources resources, Ff7WorldPresentationData presentation)
    {
        this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
        math = new(presentation);
        radar = Ff7FieldRenderer.DecodeTexture(resources.Radar);
        map = Ff7FieldRenderer.DecodeTexture(resources.Map);
        clouds = Ff7FieldRenderer.DecodeTexture(resources.Clouds);
        largeMap = Ff7FieldRenderer.DecodeTexture(resources.LargeMap);
    }

    public void DrawSky(ITriangleRenderer renderer, Ff7WorldView view, Vector3 player, int heading, int distance, int groundHeight, int roll, bool visible, int cloudPhase, Vector3 upperColor, Vector3 lowerColor)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        if (!visible)
        {
            return;
        }
        float scale = view.Height / 240f;
        Vector3 offset = Vector3.Transform(new(0, 0, -0x4000), math.Rotation(0, -heading, 0));
        Vector3 camera = Vector3.Transform(new(player.X + offset.X, 0, player.Z + offset.Z), view.NativeView);
        if (camera.Z == 0)
        {
            throw new InvalidDataException("FFVII sky projection has a zero native depth divisor.");
        }
        int horizon = (int)((view.CenterY + camera.Y * view.Focal / camera.Z) / scale) + 26;
        Matrix4x4 rotation = math.Rotation(0, 0, roll);
        float halfWidth = view.Width / (2 * scale);
        Vector2 Point(float x, float y, float centerY)
        {
            Vector3 rotated = Vector3.Transform(new(x, y, 0), rotation);
            return new(view.Width / 2f + (int)rotated.X * scale, ((int)rotated.Y + centerY) * scale);
        }
        Vector2 topLeft = Point(-halfWidth - 20, -horizon - 24, horizon);
        Vector2 topRight = Point(halfWidth + 20, -horizon - 24, horizon);
        Vector2 bottomLeft = Point(-halfWidth - 20, 0, horizon);
        Vector2 bottomRight = Point(halfWidth + 20, 0, horizon);
        Quad(renderer, null, topLeft, topRight, bottomLeft, bottomRight, Vector2.Zero, Vector2.One, new(upperColor / 255f, 1), new(lowerColor / 255f, 1), TriangleBlend.Replace);
        int cloudBottom = horizon - 44 - (5000 >> 8);
        int seam = cloudPhase - 96;
        for (int tile = (int)MathF.Floor((-halfWidth - seam) / 256); tile * 256 + seam < halfWidth; tile++)
        {
            int first = Math.Max((int)-halfWidth, seam + tile * 256);
            int last = Math.Min((int)halfWidth, seam + (tile + 1) * 256);
            if (last <= first)
            {
                continue;
            }
            float u0 = first - seam - tile * 256;
            float u1 = last - seam - tile * 256 - 1;
            Quad(renderer, clouds, Point(first, cloudBottom - 64, 40), Point(last, cloudBottom - 64, 40), Point(first, cloudBottom, 40), Point(last, cloudBottom, 40), new(u0 / resources.Clouds.Width, 0), new(u1 / resources.Clouds.Width, 63f / resources.Clouds.Height), new(127 / 255f, 127 / 255f, 127 / 255f, 128 / 255f), new(127 / 255f, 127 / 255f, 127 / 255f, 128 / 255f), TriangleBlend.AlphaAdd);
        }
    }

    public void DrawRadar(ITriangleRenderer renderer, int width, int height, int radarMode, int viewMode, int heading, uint counter, IReadOnlyList<Ff7WorldRadarMarker> markers)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        if (radarMode >= 2)
        {
            return;
        }
        if (radarMode < 0 || width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radarMode));
        }
        float scale = height / 240f;
        int size = radarMode == 1 ? 24 : 8;
        float x = radarMode == 1 ? width / 2f - 108 * scale : width - 92 * scale;
        float y = radarMode == 1 ? height / 2f - 92 * scale : height - 84 * scale;
        Ff7WorldRadarMarker? player = markers.SingleOrDefault(value => value.Controlled);
        if (viewMode is 2 or 3 && player is not null)
        {
            Vector2 center = new(x + ((size * player.X) >> 15) * scale, y + ((size * player.Z) >> 15) * scale);
            int extent = radarMode == 1 ? -16 : -8;
            Matrix4x4 rotation = math.Rotation(0, 0, heading);
            Vector3 first = Vector3.Transform(new(extent >> 1, extent, 0), rotation);
            Vector3 second = Vector3.Transform(new(-(extent >> 1), extent, 0), rotation);
            TriangleMaterial material = new(null, TriangleBlend.AlphaAdd, TriangleDepthComparison.Always, false);
            TriangleVertex a = new(center, 0, 1, Vector2.Zero, new(96 / 255f, 96 / 255f, 0, 128 / 255f));
            TriangleVertex b = new(center + new Vector2((int)first.X, (int)first.Y) * scale, 0, 1, Vector2.Zero, new(16 / 255f, 16 / 255f, 0, 128 / 255f));
            TriangleVertex c = new(center + new Vector2((int)second.X, (int)second.Y) * scale, 0, 1, Vector2.Zero, new(16 / 255f, 16 / 255f, 0, 128 / 255f));
            renderer.Draw(a, b, c, material);
        }
        if (radarMode == 0)
        {
            Rectangle(renderer, radar, x, y, 72 * scale, 56 * scale, new(1f / 128, 1f / 64), new(71f / 128, 55f / 64), new(1, 1, 1, 128 / 255f), TriangleBlend.Alpha);
        }
        else
        {
            Rectangle(renderer, largeMap, x, y, 216 * scale, 168 * scale, new(1f / 256, 1f / 256), new(215f / 256, 167f / 256), new(1, 1, 1, 128 / 255f), TriangleBlend.Alpha);
        }
        int[] modelIds = [-1, 6, 5, 3, 19, 25];
        uint[] colors = [0x80800000, 0x80006000, 0x80006060, 0x80606060, 0x80600060, 0x80606000];
        for (int index = 0; index < modelIds.Length; index++)
        {
            Ff7WorldRadarMarker? marker = index == 0 ? player : markers.SingleOrDefault(value => value.Model == modelIds[index] && !value.Controlled);
            if (marker is null)
            {
                continue;
            }
            int v = (int)((((counter + (uint)(index * 4)) >> (index == 0 ? 0 : 1)) & 16) << (radarMode == 1 ? 1 : 0)) + 4;
            Vector4 color = new((colors[index] >> 16 & 255) / 255f, (colors[index] >> 8 & 255) / 255f, (colors[index] & 255) / 255f, 128 / 255f);
            Rectangle(renderer, map, x + (((size * marker.X) >> 15) - 4) * scale, y + (((size * marker.Z) >> 15) - 4) * scale, 8 * scale, 8 * scale, new(4f / 128, v / 64f), new(12f / 128, (v + 8) / 64f), color, TriangleBlend.Alpha);
        }
    }

    private static void Rectangle(ITriangleRenderer renderer, PixelImage image, float x, float y, float width, float height, Vector2 uv0, Vector2 uv1, Vector4 color, TriangleBlend blend)
    {
        Quad(renderer, image, new(x, y), new(x + width, y), new(x, y + height), new(x + width, y + height), uv0, uv1, color, color, blend);
    }

    private static void Quad(ITriangleRenderer renderer, PixelImage? image, Vector2 topLeft, Vector2 topRight, Vector2 bottomLeft, Vector2 bottomRight, Vector2 uv0, Vector2 uv1, Vector4 topColor, Vector4 bottomColor, TriangleBlend blend)
    {
        TriangleMaterial material = new(image, blend, TriangleDepthComparison.Always, false, Sampler: TriangleSampler.PointClamp) { PreserveTargetAlpha = false };
        TriangleVertex a = new(topLeft, 0, 1, uv0, topColor);
        TriangleVertex b = new(topRight, 0, 1, new(uv1.X, uv0.Y), topColor);
        TriangleVertex c = new(bottomLeft, 0, 1, new(uv0.X, uv1.Y), bottomColor);
        TriangleVertex d = new(bottomRight, 0, 1, uv1, bottomColor);
        renderer.Draw(a, b, c, material);
        renderer.Draw(c, b, d, material);
    }
}
