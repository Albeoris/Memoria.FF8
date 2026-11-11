using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7FieldRenderer
{
    private float viewportLeft;
    private float viewportRight;

    private Ff7FieldRenderFrame PrepareViewport(int width, int height, Ff7FieldRenderFrame frame)
    {
        viewportLeft = 0;
        viewportRight = width;
        float scale = height / 240f;
        if (width <= 320 * scale || frame.MovieActive)
        {
            return frame;
        }

        PcFieldTriggers bounds = field.Triggers;
        float imageWidth = (bounds.CameraRight - bounds.CameraLeft) * scale;
        if (imageWidth <= 0)
        {
            throw new InvalidDataException("FFVII widescreen presentation requires ordered horizontal camera bounds.");
        }

        // Match FF8: clamp the reachable image to the viewport, or center it
        // when it is narrower. Keep the native simulation camera unchanged.
        float imageLeft = (width - 320 * scale) / 2 + (160 + bounds.CameraLeft + frame.CameraOffsetPixels.X) * scale;
        float left = imageWidth >= width ? Math.Clamp(imageLeft, width - imageWidth, 0) : (width - imageWidth) / 2;
        Vector2 correction = new((left - imageLeft) / scale, 0);
        viewportLeft = Math.Clamp(left, 0, width);
        viewportRight = Math.Clamp(left + imageWidth, viewportLeft, width);
        return frame with
        {
            CameraOffsetPixels = frame.CameraOffsetPixels + correction,
            PlayerPointer = frame.PlayerPointer is { } pointer ? pointer with { Position = pointer.Position + correction } : null
        };
    }
}
