using System.Numerics;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

public sealed class LightingReceiverEditorTests
{
    [Fact]
    public void Floor_and_wall_patches_place_edit_and_undo_without_touching_the_lamp_list()
    {
        LightingEditorController editor = new(FieldLightingScene.Empty("fixture"), "Fixture", Vector3.Zero, 100, _ => null, () => { });
        editor.SetProjection(point => new Vector2(point.X, point.Y), point => new Vector3(point, 5), point => new LightingEditorRay(new Vector3(point, 100), -Vector3.UnitZ));

        editor.PointerPress(new(300, 130));
        Assert.False(editor.IsDirty);
        editor.PointerPress(new(440, 130));
        editor.PointerPress(new(400, 380));
        editor.PointerPress(new(500, 450));
        FieldShadowReceiver floor = Assert.Single(editor.Draft.ShadowReceivers);
        Assert.Equal("floor", floor.Kind);
        Assert.Equal(new Vector3(450, 415, 5), floor.Center);
        Assert.Equal(100, floor.Width);
        Assert.Equal(70, floor.Extent);
        Assert.True(editor.IsDirty);

        editor.PointerPress(new(530, 130));
        editor.PointerPress(new(400, 380));
        editor.PointerPress(new(400, 480));
        FieldShadowReceiver wall = editor.Draft.ShadowReceivers[1];
        Assert.Equal("wall", wall.Kind);
        Assert.Equal(90, wall.HeadingDegrees, 4);
        Assert.Equal(new Vector3(400, 430, 80), wall.Center);

        editor.BeginPropertyEdit(LightingProperty.ReceiverHeading);
        editor.SetPropertyText("37.5");
        Assert.True(editor.CommitProperty());
        Assert.Equal(37.5f, editor.Draft.ShadowReceivers[1].HeadingDegrees);
        editor.Undo();
        Assert.Equal(wall, editor.Draft.ShadowReceivers[1]);
        editor.Undo();
        Assert.Equal(floor, Assert.Single(editor.Draft.ShadowReceivers));
        editor.Undo();
        Assert.False(editor.IsDirty);
        Assert.Empty(editor.Draft.ShadowReceivers);
        Assert.Empty(editor.Draft.Lights);
    }
}
