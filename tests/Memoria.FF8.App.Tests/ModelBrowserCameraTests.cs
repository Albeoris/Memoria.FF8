using System.Numerics;
using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class ModelBrowserCameraTests
{
    [Fact]
    public void Orbit_crosses_both_poles_and_roll_uses_the_screen_axis()
    {
        ModelPreviewCamera camera = new();
        camera.Rotate(0, MathF.PI);
        Assert.True(Vector3.Transform(Vector3.UnitY, camera.Orientation).Y < -0.99f);
        camera.Rotate(0, MathF.PI);
        Assert.True(Vector3.Distance(Vector3.UnitY, Vector3.Transform(Vector3.UnitY, camera.Orientation)) < 0.0001f);
        camera.Rotate(0, 0, MathF.PI / 2);
        Assert.True(Vector3.Distance(-Vector3.UnitX, Vector3.Transform(Vector3.UnitY, camera.Orientation)) < 0.0001f);
    }

    [Fact]
    public void Drag_captures_the_preview_until_release_and_does_not_start_over_the_list()
    {
        ModelBrowserController browser = Browser();
        browser.OpenAssetPanel();
        browser.UpdatePreviewPointer(600, 200, true, false, false, false, true);
        browser.UpdatePreviewPointer(100, 200, true, false, false, false, true);
        Assert.False(browser.PreviewDragging);
        Assert.Equal(Quaternion.Identity, browser.Camera.Orientation);
        browser.UpdatePreviewPointer(100, 200, false, false, false, false, true);
        browser.UpdatePreviewPointer(100, 200, true, false, false, false, true);
        browser.UpdatePreviewPointer(700, 800, true, false, false, false, true);
        Assert.True(browser.PreviewDragging);
        Assert.True(browser.PreviewFocused);
        Assert.NotEqual(Quaternion.Identity, browser.Camera.Orientation);
        browser.UpdatePreviewPointer(700, 800, false, false, false, false, true);
        Quaternion released = browser.Camera.Orientation;
        browser.UpdatePreviewPointer(100, 200, false, false, false, false, true);
        Assert.False(browser.PreviewDragging);
        Assert.Equal(released, browser.Camera.Orientation);
    }

    [Fact]
    public void Pan_and_roll_are_available_in_selection_and_reset_restores_framing()
    {
        ModelBrowserController browser = Browser();
        browser.OpenAssetPanel();
        browser.UpdatePreviewPointer(100, 200, false, true, false, true, true);
        browser.UpdatePreviewPointer(136, 218, false, true, false, true, true);
        Assert.Equal(new Vector2(0.1f, 0.05f), browser.Camera.Pan);
        browser.UpdatePreviewPointer(136, 218, false, false, false, false, true);
        browser.UpdatePreviewPointer(100, 200, false, false, true, false, true);
        browser.UpdatePreviewPointer(120, 180, false, false, true, false, true);
        Assert.NotEqual(Quaternion.Identity, browser.Camera.Orientation);
        Assert.True(browser.Zoom > 1);
        browser.UpdatePreviewPointer(120, 180, false, false, true, false, false);
        Assert.False(browser.PreviewDragging);
        browser.ResetView();
        Assert.Equal(Quaternion.Identity, browser.Camera.Orientation);
        Assert.Equal(Vector2.Zero, browser.Camera.Pan);
        Assert.Equal(1, browser.Zoom);
    }

    [Fact]
    public void Middle_drag_orbits_and_shift_left_drag_pans()
    {
        ModelBrowserController browser = Browser();
        browser.UpdatePreviewPointer(100, 200, false, true, false, false, true);
        browser.UpdatePreviewPointer(130, 220, false, true, false, false, true);
        Assert.NotEqual(Quaternion.Identity, browser.Camera.Orientation);
        Assert.Equal(Vector2.Zero, browser.Camera.Pan);
        browser.UpdatePreviewPointer(130, 220, false, false, false, false, true);
        browser.UpdatePreviewPointer(100, 200, true, false, false, true, true);
        browser.UpdatePreviewPointer(130, 220, true, false, false, true, true);
        Assert.NotEqual(Vector2.Zero, browser.Camera.Pan);
    }

    [Fact]
    public void Presets_keep_framing_while_reset_restores_the_whole_view()
    {
        ModelBrowserController browser = Browser();
        browser.Camera.PanBy(1, 2);
        browser.Camera.ZoomBy(2);
        browser.SetCameraView(ModelPreviewView.Side);
        Assert.True(Vector3.Transform(Vector3.UnitX, browser.Camera.Orientation).Z < -0.99f);
        Assert.Equal(new Vector2(1, 2), browser.Camera.Pan);
        Assert.Equal(2, browser.Zoom);
        browser.SetCameraView(ModelPreviewView.Top);
        Assert.True(Vector3.Transform(Vector3.UnitY, browser.Camera.Orientation).Z > 0.99f);
        browser.SetCameraView(ModelPreviewView.Front);
        Assert.Equal(Quaternion.Identity, browser.Camera.Orientation);
        browser.SetCameraView(ModelPreviewView.Reset);
        Assert.Equal(Vector2.Zero, browser.Camera.Pan);
        Assert.Equal(1, browser.Zoom);
    }

    private static ModelBrowserController Browser()
    {
        ModelBrowserController browser = new([new("model", "Model", "Field", "Model", [new("original", "Original")])], () => { }, (_, _) => { }, (_, _) => { });
        browser.OpenViewer();
        return browser;
    }
}
