using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7FieldRenderer
{
    private void DrawWaterSplashes(ITriangleRenderer renderer, int width, int height, Ff7FieldRenderFrame frame, Ff7ModelMaterialState precedingState)
    {
        foreach (Ff7FieldWaterSplashVisual splash in frame.WaterSplashes.OrderBy(splash => splash.TextureIndex))
        {
            if ((uint)splash.TextureIndex >= field.WaterSplashTextures.Count || !float.IsFinite(splash.Position.X) || !float.IsFinite(splash.Position.Y) || !float.IsFinite(splash.Depth) || !float.IsFinite(splash.HalfSize) || splash.HalfSize <= 0)
            {
                throw new InvalidDataException("Native field splash geometry requires its original texture queue and finite cached coordinates.");
            }
            PcTexture texture = field.WaterSplashTextures[splash.TextureIndex];
            if (!modelImages.TryGetValue(texture, out PixelImage? image))
            {
                image = DecodeTexture(texture, useColorKey: true);
                modelImages.Add(texture, image);
            }
            TriangleSampler sampler = precedingState.Sampler is TriangleSampler.PointClamp or TriangleSampler.LinearClamp or TriangleSampler.CutoutLinearClamp ? TriangleSampler.PointClamp : TriangleSampler.PointWrap;
            TriangleMaterial material = new(image, TriangleBlend.Alpha, precedingState.DepthTest ? TriangleDepthComparison.LessEqual : TriangleDepthComparison.Always, precedingState.DepthWrite, precedingState.AlphaReference, sampler) { PreserveTargetAlpha = false, ClipRectangle = FieldClip(width, height) };
            Vector2 origin = splash.Position - new Vector2(splash.HalfSize);
            Vector2 size = new(splash.HalfSize * 2 * height / 240f);
            TrianglePrimitives.Quad(renderer, FieldPoint(origin, width, height), size, splash.Depth, new Vector4(1, 1, 1, 128 / 255f), material, Vector2.One);
        }
    }
}
