using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

internal sealed class LightingEditorViewport
{
    private static readonly Vector2 DesignSize = new(LightingEditorController.DesignWidth, LightingEditorController.DesignHeight);
    private static readonly Vector2 DesignCenter = DesignSize * 0.5f;
    private Vector2 pan;

    internal float Zoom { get; private set; } = 1;

    internal void Reset()
    {
        Zoom = 1;
        pan = Vector2.Zero;
    }

    internal void Pan(Vector2 delta)
    {
        pan += delta;
    }

    internal void ZoomAt(Vector2 pointer, float wheelSteps)
    {
        float nextZoom = Math.Clamp(Zoom * MathF.Pow(1.2f, wheelSteps), 0.5f, 4);
        if (nextZoom == Zoom)
        {
            return;
        }
        Vector2 fromCenter = pointer - DesignCenter;
        pan = fromCenter - (fromCenter - pan) * (nextZoom / Zoom);
        Zoom = nextZoom;
    }

    internal static Vector2 WindowToDesign(Vector2 point, int width, int height)
    {
        float scale = DesignScale(width, height);
        return (point - DesignOrigin(width, height, scale)) / scale;
    }

    internal LightingEditorViewportMapping Map(int width, int height, int fieldWidth, int fieldHeight)
    {
        PixelBounds fit = ViewportLayout.Fit(fieldWidth, fieldHeight, width, height);
        float designScale = DesignScale(width, height);
        Vector2 designOrigin = DesignOrigin(width, height, designScale);
        Vector2 windowCenter = designOrigin + DesignCenter * designScale;
        Vector2 frameOrigin = windowCenter + (new Vector2(fit.X, fit.Y) - windowCenter) * Zoom + pan * designScale;
        Vector2 fieldScale = new Vector2(fit.Width / (float)fieldWidth, fit.Height / (float)fieldHeight) * Zoom;
        return new LightingEditorViewportMapping(frameOrigin, fieldScale, designOrigin, designScale);
    }

    private static float DesignScale(int width, int height)
    {
        return Math.Min(width / DesignSize.X, height / DesignSize.Y);
    }

    private static Vector2 DesignOrigin(int width, int height, float scale)
    {
        return (new Vector2(width, height) - DesignSize * scale) * 0.5f;
    }
}

internal readonly record struct LightingEditorViewportMapping(Vector2 FrameOrigin, Vector2 FieldScale, Vector2 DesignOrigin, float DesignScale)
{
    internal Vector2 FieldToDesign(Vector2 point)
    {
        return (FrameOrigin + point * FieldScale - DesignOrigin) / DesignScale;
    }

    internal Vector2 DesignToField(Vector2 point)
    {
        return (point * DesignScale + DesignOrigin - FrameOrigin) / FieldScale;
    }
}
