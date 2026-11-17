using System.Numerics;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class LightingEditorViewportTests
{
    [Theory]
    [InlineData(1611, 903, 1024, 512)]
    [InlineData(1441, 1083, 320, 240)]
    public void Zoom_anchor_pan_and_picking_agree_with_the_composited_field(int width, int height, int fieldWidth, int fieldHeight)
    {
        LightingEditorViewport viewport = new();
        Vector2 fieldPoint = new(fieldWidth * 0.62f, fieldHeight * 0.41f);
        LightingEditorViewportMapping fit = viewport.Map(width, height, fieldWidth, fieldHeight);
        PixelBounds expectedFit = ViewportLayout.Fit(fieldWidth, fieldHeight, width, height);
        Assert.Equal(new Vector2(expectedFit.X, expectedFit.Y), fit.FrameOrigin);
        Vector2 anchor = fit.FieldToDesign(fieldPoint);

        viewport.ZoomAt(anchor, 3);
        AssertPoint(anchor, viewport.Map(width, height, fieldWidth, fieldHeight).FieldToDesign(fieldPoint));
        Vector2 pan = new(-102, 63);
        viewport.Pan(pan);
        LightingEditorViewportMapping zoomed = viewport.Map(width, height, fieldWidth, fieldHeight);
        Vector2 windowPoint = zoomed.FrameOrigin + fieldPoint * zoomed.FieldScale;
        Vector2 pointer = LightingEditorViewport.WindowToDesign(windowPoint, width, height);
        AssertPoint(anchor + pan, pointer);
        AssertPoint(pointer, zoomed.FieldToDesign(fieldPoint));
        AssertPoint(fieldPoint, zoomed.DesignToField(pointer));

        viewport.Reset();
        Assert.Equal(fit, viewport.Map(width, height, fieldWidth, fieldHeight));
    }

    [Fact]
    public void Viewport_gestures_leave_document_and_undo_history_untouched()
    {
        LightingEditorController editor = new(FieldLightingScene.Empty("test"), "Test", Vector3.Zero, 100, _ => null, () => { });
        editor.Poll(new(), Mouse(602, 402, 120, ButtonState.Released), true, 1280, 720);
        Assert.InRange(editor.Viewport.Zoom, 1.19f, 1.21f);
        LightingEditorViewportMapping zoomed = editor.Viewport.Map(1280, 720, 1280, 720);

        editor.Poll(new(), Mouse(602, 402, 120, ButtonState.Pressed), true, 1280, 720);
        editor.Poll(new(), Mouse(649, 416, 120, ButtonState.Pressed), true, 1280, 720);
        editor.Poll(new(), Mouse(649, 416, 120, ButtonState.Released), true, 1280, 720);
        AssertPoint(zoomed.FrameOrigin + new Vector2(47, 14), editor.Viewport.Map(1280, 720, 1280, 720).FrameOrigin);

        editor.PointerPress(new(LightingEditorLayout.ResetView.X + 2, LightingEditorLayout.ResetView.Y + 2));
        Assert.Equal(1, editor.Viewport.Zoom);
        Assert.Equal(Vector2.Zero, editor.Viewport.Map(1280, 720, 1280, 720).FrameOrigin);
        Assert.False(editor.IsDirty);
        Assert.Equal(0, editor.History.CommandCount);
    }

    [Fact]
    public void Scrolling_down_from_fit_shrinks_the_frame_around_the_cursor()
    {
        LightingEditorController editor = new(FieldLightingScene.Empty("test"), "Test", Vector3.Zero, 100, _ => null, () => { });
        Vector2 pointer = new(602, 402);
        LightingEditorViewportMapping fit = editor.Viewport.Map(1280, 720, 800, 600);
        Vector2 fieldPoint = fit.DesignToField(pointer);

        editor.Poll(new(), Mouse(602, 402, -120, ButtonState.Released), true, 1280, 720);
        LightingEditorViewportMapping zoomed = editor.Viewport.Map(1280, 720, 800, 600);
        Assert.InRange(editor.Viewport.Zoom, 0.82f, 0.84f);
        Assert.True(zoomed.FieldScale.X < fit.FieldScale.X && zoomed.FieldScale.Y < fit.FieldScale.Y);
        AssertPoint(pointer, zoomed.FieldToDesign(fieldPoint));

        editor.Poll(new(), Mouse(602, 402, -1200, ButtonState.Released), true, 1280, 720);
        Assert.Equal(0.5f, editor.Viewport.Zoom);
        AssertPoint(pointer, editor.Viewport.Map(1280, 720, 800, 600).FieldToDesign(fieldPoint));
        editor.Viewport.Reset();
        Assert.Equal(fit, editor.Viewport.Map(1280, 720, 800, 600));
    }

    private static MouseState Mouse(int x, int y, int scroll, ButtonState middle)
    {
        return new(x, y, scroll, ButtonState.Released, middle, ButtonState.Released, ButtonState.Released, ButtonState.Released);
    }

    private static void AssertPoint(Vector2 expected, Vector2 actual)
    {
        Assert.InRange(Vector2.Distance(expected, actual), 0, 0.001f);
    }
}
