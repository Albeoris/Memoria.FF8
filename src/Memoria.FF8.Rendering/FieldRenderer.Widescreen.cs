using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class FieldRenderer
{
    private readonly bool widescreen;
    private int originalClipLeft;
    private int originalClipRight;
    private float horizontalPresentationOffset;

    private float HorizontalOrigin => offsetX + horizontalPresentationOffset;

    public PixelBounds ContentBounds => new(clipLeft, clipTop, clipRight - clipLeft, clipBottom - clipTop);

    private bool UpdateHorizontalViewport(int cameraIndex, Vector2 scroll)
    {
        int left = Math.Clamp((int)MathF.Ceiling(offsetX - 0.5f), 0, OutputWidth);
        int right = Math.Clamp((int)MathF.Ceiling(offsetX + Width * scale - 0.5f), 0, OutputWidth);
        float previousOffset = horizontalPresentationOffset;
        horizontalPresentationOffset = 0;
        if (widescreen && field.Information is { ScreenBounds.Count: > 0 } information && (uint)cameraIndex < information.CameraBounds.Count)
        {
            FieldBounds bounds = information.CameraBounds[cameraIndex];
            // The selected INF camera clamp defines the reachable image. A
            // parallax layer's tiles or wrap period do not extend that image.
            float imageLeft = offsetX + (drawingOrigin.X + bounds.Left + scroll.X) * scale;
            float imageWidth = (bounds.Right - bounds.Left) * scale;
            if (OutputWidth > Width * scale && imageWidth > 0)
            {
                // Keep the visible camera inside the image, centering images
                // narrower than the target instead of following past either edge.
                float clampedLeft = imageWidth >= OutputWidth ? Math.Clamp(imageLeft, OutputWidth - imageWidth, 0) : (OutputWidth - imageWidth) / 2;
                horizontalPresentationOffset = clampedLeft - imageLeft;
                left = Math.Clamp((int)MathF.Ceiling(imageLeft + horizontalPresentationOffset - 0.5f), 0, OutputWidth);
                right = Math.Clamp((int)MathF.Ceiling(clampedLeft + imageWidth - 0.5f), left, OutputWidth);
            }
            else
            {
                left = Math.Min(left, Math.Clamp((int)MathF.Ceiling(imageLeft - 0.5f), 0, OutputWidth));
                right = Math.Max(right, Math.Clamp((int)MathF.Ceiling(imageLeft + imageWidth - 0.5f), 0, OutputWidth));
            }
        }
        bool changed = clipLeft != left || clipRight != right || previousOffset != horizontalPresentationOffset;
        clipLeft = left;
        clipRight = right;
        originalClipLeft = Math.Clamp((int)MathF.Ceiling(HorizontalOrigin - 0.5f), clipLeft, clipRight);
        originalClipRight = Math.Clamp((int)MathF.Ceiling(HorizontalOrigin + Width * scale - 0.5f), originalClipLeft, clipRight);
        return changed;
    }

    private void AddWidescreenBackgroundRasters(List<TileRaster> rasters, IReadOnlyDictionary<int, Vector2> offsets, FieldNativeBackgroundWrapFrame? frame, Vector2 jitter, int cameraIndex)
    {
        if (clipLeft == originalClipLeft && clipRight == originalClipRight)
        {
            return;
        }
        // Keep the original cull, wrapping and CLUT ordinals in the central
        // viewport. Extra copies are confined to disjoint presentation strips.
        FieldBounds screen = field.Information!.ScreenBounds[0];
        int halfWidth = (screen.Right - screen.Left) / 2;
        int halfHeight = (screen.Bottom - screen.Top) / 2;
        FieldBounds bounds = field.Information.CameraBounds[cameraIndex];
        float imageLeft = bounds.Left + actorScrollOffset.X - jitter.X;
        float imageRight = Math.Max(bounds.Right, bounds.Left + halfWidth * 2) + actorScrollOffset.X - jitter.X;
        AddStrips(clipLeft, originalClipLeft);
        AddStrips(originalClipRight, clipRight);

        void AddStrips(int left, int right)
        {
            int maximumWidth = Math.Max(1, (int)MathF.Floor(halfWidth * 2 * scale));
            for (int start = left; start < right; start += maximumWidth)
            {
                AddStrip(start, Math.Min(start + maximumWidth, right));
            }
        }

        void AddStrip(int left, int right)
        {
            if (left >= right)
            {
                return;
            }
            float center = ((left + right) / 2f - HorizontalOrigin) / scale - drawingOrigin.X;
            center = Math.Clamp(center, imageLeft + halfWidth, imageRight - halfWidth);
            Dictionary<ushort, int> paletteOrders = [];
            int previousDepth = background.Length == 0 ? 0 : background[0].Tile.Z;
            if (background.Length != 0)
            {
                paletteOrders.Add(background[0].Tile.PackedPalette, 0);
            }
            foreach (TileRaster raster in background)
            {
                BackgroundTile tile = raster.Tile;
                Vector2 scroll = offsets.GetValueOrDefault(tile.Layer);
                float x = tile.X;
                float y = tile.Y;
                int period = 0;
                FieldNativeBackgroundWrapLayer? wrapping = frame?.Layers.Where(layer => layer.Layer == tile.Layer).Select(layer => (FieldNativeBackgroundWrapLayer?)layer).FirstOrDefault();
                if (TryPresentationPlacement(tile, out Vector2 presentationOrigin))
                {
                    x = presentationOrigin.X;
                    y = presentationOrigin.Y;
                    period = wrapping?.PeriodX ?? 0;
                }
                else if (frame is not null && wrapping is FieldNativeBackgroundWrapLayer layer)
                {
                    FieldNativeBackgroundTileOrigin? origin = FieldNativeBackgroundTileProbe.Relocate(checked((short)tile.X), checked((short)tile.Y), layer, frame, cullHorizontal: false);
                    if (origin is not FieldNativeBackgroundTileOrigin placed)
                    {
                        continue;
                    }
                    x = placed.X;
                    y = placed.Y;
                    period = layer.PeriodX;
                }
                float windowY = y + scroll.Y - jitter.Y;
                if (windowY <= -halfHeight - 32 || windowY >= halfHeight + 16)
                {
                    continue;
                }
                float minimumX = center - halfWidth - 32 - scroll.X + jitter.X;
                float maximumX = center + halfWidth + 16 - scroll.X + jitter.X;
                int first = period > 0 ? (int)MathF.Floor((minimumX - x) / period) + 1 : 0;
                int last = period > 0 ? (int)MathF.Ceiling((maximumX - x) / period) - 1 : 0;
                for (int copy = first; copy <= last; copy++)
                {
                    float placedX = x + copy * period;
                    if (placedX <= minimumX || placedX >= maximumX)
                    {
                        continue;
                    }
                    if (tile.Z != previousDepth)
                    {
                        paletteOrders.Clear();
                        previousDepth = tile.Z;
                    }
                    if (!paletteOrders.TryGetValue(tile.PackedPalette, out int order))
                    {
                        order = paletteOrders.Count;
                        if (order >= 16)
                        {
                            throw new NotSupportedException("Native MAP depth group exceeds its sixteen CLUT slots.");
                        }
                        paletteOrders.Add(tile.PackedPalette, order);
                    }
                    float pixelLeft = HorizontalOrigin + (placedX + drawingOrigin.X + scroll.X) * scale;
                    if (pixelLeft + 16 * scale <= left || pixelLeft >= right)
                    {
                        continue;
                    }
                    Vector2 placement = scroll + new Vector2(placedX - tile.X, y - tile.Y);
                    rasters.Add(CreateTileRaster(tile, placement, left: left, right: right) with { Depth = tile.Z * 4f + order / 4f });
                }
            }
        }
    }
}
