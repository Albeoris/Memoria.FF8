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
        TrianglePrimitives.Quad(TriangleRenderer!, origin, size, depth, color, material, uvEnd, uvStart, pointSampleRectangle);
    }

    private Vector3 BackgroundTileShade(BackgroundTile tile)
    {
        // Native MAP byte 14 indexes the module RGB/frame array directly; 255 has no module.
        return moduleColors is not null && tile.AnimationId != 255 && moduleColors.TryGetValue(tile.AnimationId, out Vector3 shade) ? shade : backgroundColor;
    }

    private void DrawGpuTile(TileRaster raster, Vector3 shade, bool translucentPass, InspectionBackdropQuad? inspection = null)
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
            MinimumAlpha = blended && !translucentPass ? 0.75f : 1f / 255,
            MaximumAlpha = translucentPass ? 0.75f : 1
        };
        Vector4 color = new(shade / 128, 1);
        if (inspection is { } quad)
        {
            // Keep point samples at a projected tile edge within its atlas region.
            Vector2 inset = new(0.001f / region.Image.Width, 0.001f / region.Image.Height);
            Vector2 regionSize = region.End - region.Start;
            Vector2 start = Vector2.Clamp(region.Start + regionSize * quad.TextureStart, region.Start + inset, region.End - inset);
            Vector2 end = Vector2.Clamp(region.Start + regionSize * quad.TextureEnd, region.Start + inset, region.End - inset);
            DrawGpuInspectionBackdrop(quad, start, end, color, material with { PerspectiveDepth = true, CentroidTextureSampling = true });
            return;
        }
        GpuQuad(raster.Origin, raster.Size, raster.Depth, color, material with { ClipRectangle = raster.Clip }, region.End, region.Start, pointSampleRectangle: true);
    }

    private void DrawGpuInspectionBackdrop(InspectionBackdropQuad tile, Vector2 uvStart, Vector2 uvEnd, Vector4 color, TriangleMaterial material)
    {
        TriangleVertex a = GpuVertex(tile.A, uvStart, color);
        TriangleVertex b = GpuVertex(tile.B, new Vector2(uvEnd.X, uvStart.Y), color);
        TriangleVertex c = GpuVertex(tile.C, new Vector2(uvStart.X, uvEnd.Y), color);
        TriangleVertex d = GpuVertex(tile.D, uvEnd, color);
        TriangleRenderer!.Draw(a, b, c, material);
        TriangleRenderer.Draw(b, d, c, material);
    }

    private void DrawGpuFade(FieldFadeSample? fade)
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
        Vector4 color = fade.Mode == NativeFadeDrawMode.Black ? new Vector4(0, 0, 0, 1) : new Vector4(fade.Color / 255, 1);
        TriangleMaterial material = new(null, blend, TriangleDepthComparison.Always, false) { ClipRectangle = new Vector4(clipLeft, clipTop, clipRight, clipBottom) };
        GpuQuad(Vector2.Zero, new Vector2(OutputWidth, OutputHeight), 0, color, material, Vector2.One);
    }
}
