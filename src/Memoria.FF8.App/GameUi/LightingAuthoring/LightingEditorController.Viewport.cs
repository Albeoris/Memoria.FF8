using System.Numerics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.GameUi;

public sealed partial class LightingEditorController
{
    private Vector2? previousPanPointer;

    internal LightingEditorViewport Viewport { get; } = new();

    private bool HandleViewportInput(KeyboardState keys, MouseState mouse, MouseState oldMouse)
    {
        if (keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt) || keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift) || keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl))
        {
            EndViewportPan();
            return false;
        }

        if (previousPanPointer is { } previous)
        {
            Viewport.Pan(pointer - previous);
            previousPanPointer = mouse.MiddleButton == ButtonState.Pressed ? pointer : null;
            return true;
        }

        if (!IsViewportPointer(pointer))
        {
            return false;
        }
        if (mouse.MiddleButton == ButtonState.Pressed && oldMouse.MiddleButton == ButtonState.Released)
        {
            previousPanPointer = pointer;
            return true;
        }

        int wheelDelta = mouse.ScrollWheelValue - oldMouse.ScrollWheelValue;
        if (wheelDelta != 0)
        {
            Viewport.ZoomAt(pointer, wheelDelta / 120f);
            return true;
        }
        return false;
    }

    private void EndViewportPan()
    {
        previousPanPointer = null;
    }

    internal bool IsViewportPointer(Vector2 point)
    {
        return point.X >= 0 && point.X < DesignWidth && point.Y >= 0 && point.Y < DesignHeight
            && !LightingEditorLayout.Header.Contains(point.X, point.Y)
            && !LightingEditorLayout.Footer.Contains(point.X, point.Y)
            && !LightingEditorLayout.Sources.Contains(point.X, point.Y)
            && !LightingEditorLayout.Properties.Contains(point.X, point.Y)
            && !LightingEditorLayout.Move.Contains(point.X, point.Y)
            && !LightingEditorLayout.Rotate.Contains(point.X, point.Y)
            && !LightingEditorLayout.Orbit.Contains(point.X, point.Y)
            && !LightingEditorLayout.Pivot.Contains(point.X, point.Y)
            && !LightingEditorLayout.ResetView.Contains(point.X, point.Y)
            && !ReceiverControlsContain(point)
            && !InspectionControlsContain(point);
    }
}
