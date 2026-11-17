using System.Numerics;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public sealed partial class FieldRenderer
{
    private Matrix4x4? inspectionBackdropToWorld;
    private Vector2 inspectionBackdropOrigin;
    private Vector2 inspectionBackdropRayScale;

    /// <summary>Preview native screen tiles as depth-ordered planes while the lighting inspection camera is active.</summary>
    public bool InspectionBackdropEnabled { get; set; }

    private bool ShouldDrawBackgroundTiles => InspectionCamera is null || inspectionBackdropToWorld.HasValue;

    private void PrepareInspectionBackdrop(FieldCamera nativeCamera)
    {
        inspectionBackdropToWorld = null;
        if (!InspectionBackdropEnabled || InspectionCamera is null || TriangleRenderer is null || nativeCamera.FocalLength <= 0)
        {
            return;
        }
        Matrix4x4 view = new(nativeCamera.AxisX.X, nativeCamera.AxisY.X, nativeCamera.AxisZ.X, 0, nativeCamera.AxisX.Y, nativeCamera.AxisY.Y, nativeCamera.AxisZ.Y, 0, nativeCamera.AxisX.Z, nativeCamera.AxisY.Z, nativeCamera.AxisZ.Z, 0, nativeCamera.Translation.X, nativeCamera.Translation.Y, nativeCamera.Translation.Z, 1);
        if (!Matrix4x4.Invert(view, out Matrix4x4 inverse))
        {
            return;
        }
        Vector2 viewportScale = new(scale, verticalScale);
        inspectionBackdropToWorld = inverse;
        inspectionBackdropOrigin = new Vector2(HorizontalOrigin, offsetY) + (drawingOrigin + actorScrollOffset + nativeCamera.Pan) * viewportScale;
        inspectionBackdropRayScale = Vector2.One / (viewportScale * nativeCamera.FocalLength);
    }

    private void DrawInspectionBackdropTile(TileRaster raster, List<(float Depth, Action Draw)> translucent)
    {
        if (TryProjectInspectionBackdrop(raster) is not { } tile)
        {
            return;
        }
        Vector3 shade = BackgroundTileShade(raster.Tile);
        DrawGpuTile(raster, shade, false, tile);
        if (raster.Tile.Blend && raster.Tile.BlendMode != MapBlendMode.None)
        {
            translucent.Add((tile.SortDepth, () => DrawGpuTile(raster, shade, true, tile)));
        }
    }

    private InspectionBackdropQuad? TryProjectInspectionBackdrop(TileRaster raster)
    {
        // CLUT ordinals control native overlap, but must not separate adjacent tiles in the same depth plane.
        float planeDepth = raster.Tile.Z * 4f;
        if (inspectionBackdropToWorld is not { } toWorld || planeDepth <= 1)
        {
            return null;
        }

        Vector2 topLeft = Vector2.Max(raster.Origin, new Vector2(raster.Clip.X, raster.Clip.Y));
        Vector2 bottomRight = Vector2.Min(raster.Origin + raster.Size, new Vector2(raster.Clip.Z, raster.Clip.W));
        if (bottomRight.X <= topLeft.X || bottomRight.Y <= topLeft.Y)
        {
            return null;
        }

        // MAP tiles contain ordering depth in screen space. One camera-facing plane per tile is an inspection approximation.
        ProjectedPoint Project(Vector2 screen)
        {
            Vector2 ray = (screen - inspectionBackdropOrigin) * inspectionBackdropRayScale;
            Vector3 position = Vector3.Transform(new Vector3(ray * planeDepth, planeDepth), toWorld);
            return ToViewport(camera.Project(position));
        }

        ProjectedPoint a = Project(topLeft);
        ProjectedPoint b = Project(new Vector2(bottomRight.X, topLeft.Y));
        ProjectedPoint c = Project(new Vector2(topLeft.X, bottomRight.Y));
        ProjectedPoint d = Project(bottomRight);
        if (!IsVisible(a) || !IsVisible(b) || !IsVisible(c) || !IsVisible(d))
        {
            return null;
        }
        return new InspectionBackdropQuad(a, b, c, d, (topLeft - raster.Origin) / raster.Size, (bottomRight - raster.Origin) / raster.Size);
    }

    private static bool IsVisible(ProjectedPoint point)
    {
        return point.Depth > 1 && float.IsFinite(point.Depth) && float.IsFinite(point.X) && float.IsFinite(point.Y);
    }

    private readonly record struct InspectionBackdropQuad(ProjectedPoint A, ProjectedPoint B, ProjectedPoint C, ProjectedPoint D, Vector2 TextureStart, Vector2 TextureEnd)
    {
        internal float SortDepth => (A.Depth + B.Depth + C.Depth + D.Depth) * 0.25f;
    }
}
