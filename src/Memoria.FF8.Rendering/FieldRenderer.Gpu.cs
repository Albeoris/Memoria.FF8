using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class FieldRenderer
{
    private TriangleMaterial GpuMaterial(PixelImage? texture, TriangleBlend blend, bool depthWrite)
    {
        return new TriangleMaterial(texture, blend, TriangleDepthComparison.LessEqual, depthWrite, Sampler: TriangleSampler.PointClamp)
        {
            DepthMapping = TriangleDepthMapping.Positive,
            ClipRectangle = new Vector4(clipLeft, clipTop, clipRight, clipBottom)
        };
    }

    private static TriangleVertex GpuVertex(ProjectedPoint point, Vector2 uv, Vector4 color, float? depthOverride = null, bool perspective = true)
    {
        return new TriangleVertex(new Vector2(point.X, point.Y), depthOverride ?? point.Depth, perspective ? 1 / point.Depth : 1, uv, color);
    }

    private void GpuQuad(Vector2 origin, Vector2 size, float depth, Vector4 color, TriangleMaterial material, Vector2 uvEnd, Vector2 uvStart = default, bool pointSampleRectangle = false)
    {
        Vector4 screenRectangle = default;
        Vector4 texelRectangle = default;
        if (pointSampleRectangle && material.Texture is { } texture)
        {
            Vector2 textureSize = new(texture.Width, texture.Height);
            screenRectangle = new(origin, size.X, size.Y);
            Vector2 texelOrigin = uvStart * textureSize;
            Vector2 texelSize = (uvEnd - uvStart) * textureSize;
            texelRectangle = new(texelOrigin, texelSize.X, texelSize.Y);
        }
        TriangleVertex a = new(origin, depth, 1, uvStart, color) { TextureScreenRectangle = screenRectangle, TextureTexelRectangle = texelRectangle };
        TriangleVertex b = new(origin + new Vector2(size.X, 0), depth, 1, new Vector2(uvEnd.X, uvStart.Y), color) { TextureScreenRectangle = screenRectangle, TextureTexelRectangle = texelRectangle };
        TriangleVertex c = new(origin + new Vector2(0, size.Y), depth, 1, new Vector2(uvStart.X, uvEnd.Y), color) { TextureScreenRectangle = screenRectangle, TextureTexelRectangle = texelRectangle };
        TriangleVertex d = new(origin + size, depth, 1, uvEnd, color) { TextureScreenRectangle = screenRectangle, TextureTexelRectangle = texelRectangle };
        TriangleRenderer!.Draw(a, b, c, material);
        TriangleRenderer.Draw(b, d, c, material);
    }

    private void DrawGpuTile(TileRaster raster, Vector3 shade, bool translucentPass)
    {
        BackgroundTile tile = raster.Tile;
        bool blended = tile.Blend && tile.BlendMode != MapBlendMode.None;
        if (translucentPass && !blended)
        {
            return;
        }
        TriangleBlend blend = !translucentPass ? TriangleBlend.Replace : tile.BlendMode switch
        {
            MapBlendMode.Average => TriangleBlend.HalfAdd,
            MapBlendMode.Add => TriangleBlend.Add,
            MapBlendMode.Subtract => TriangleBlend.Subtract,
            MapBlendMode.QuarterAdd => TriangleBlend.QuarterAdd,
            _ => TriangleBlend.Replace
        };
        BackgroundAtlasRegion region = GpuBackgroundRegion(tile.ResolvedImage);
        TriangleMaterial material = GpuMaterial(region.Image, blend, !translucentPass) with
        {
            ClipRectangle = raster.Clip,
            MinimumAlpha = blended && !translucentPass ? 0.75f : 1f / 255,
            MaximumAlpha = translucentPass ? 0.75f : 1
        };
        GpuQuad(raster.Origin, raster.Size, raster.Depth, new Vector4(shade / 128, 1), material, region.End, region.Start, pointSampleRectangle: true);
    }

    private void DrawGpuFade(NativeFadeDraw? fade)
    {
        if (fade is null || fade.Mode == NativeFadeDrawMode.None)
        {
            return;
        }
        TriangleBlend blend = fade.Mode switch
        {
            NativeFadeDrawMode.Add => TriangleBlend.Add,
            NativeFadeDrawMode.Subtract => TriangleBlend.Subtract,
            _ => TriangleBlend.Replace
        };
        Vector4 color = fade.Mode == NativeFadeDrawMode.Black ? new Vector4(0, 0, 0, 1) : new Vector4(fade.Red / 255f, fade.Green / 255f, fade.Blue / 255f, 1);
        TriangleMaterial material = new(null, blend, TriangleDepthComparison.Always, false) { ClipRectangle = new Vector4(clipLeft, clipTop, clipRight, clipBottom) };
        GpuQuad(Vector2.Zero, new Vector2(OutputWidth, OutputHeight), 0, color, material, Vector2.One);
    }
}
