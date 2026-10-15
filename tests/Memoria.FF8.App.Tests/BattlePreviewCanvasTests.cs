using System.Drawing;
using System.Drawing.Imaging;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattlePreviewCanvasTests
{
    [Fact]
    public void Rgba_conversion_preserves_channel_order_and_transparency()
    {
        using Bitmap bitmap = BattlePreviewCanvas.BitmapFromRgba([17, 45, 93, 128, 201, 151, 101, 255], 2, 1);
        Assert.Equal(Color.FromArgb(128, 17, 45, 93), bitmap.GetPixel(0, 0));
        Assert.Equal(Color.FromArgb(255, 201, 151, 101), bitmap.GetPixel(1, 0));
    }

    [Fact]
    public void Nested_clips_intersect_and_restore_parent_clip()
    {
        using Bitmap bitmap = new(8, 8, PixelFormat.Format32bppArgb);
        using (BattlePreviewCanvas canvas = new(bitmap))
        {
            canvas.PushClip(new UiRect(1, 1, 5, 5));
            canvas.Fill(new UiRect(0, 0, 8, 8), new UiColor(255, 0, 0));
            canvas.PushClip(new UiRect(4, 4, 4, 4));
            canvas.Fill(new UiRect(0, 0, 8, 8), new UiColor(0, 255, 0));
            canvas.PopClip();
            canvas.Fill(new UiRect(2, 2, 1, 1), new UiColor(0, 0, 255));
            canvas.PopClip();
        }
        Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(0, 0).ToArgb());
        Assert.Equal(Color.Red.ToArgb(), bitmap.GetPixel(1, 1).ToArgb());
        Assert.Equal(Color.Lime.ToArgb(), bitmap.GetPixel(4, 4).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), bitmap.GetPixel(2, 2).ToArgb());
        Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(6, 6).ToArgb());
    }

    [Fact]
    public void Shared_live_hud_renders_to_cpu_bitmap_without_a_graphics_device()
    {
        BattleSession session = new(new BattleDefinition
        {
            Combatants = [new() { Id = "party", Name = "Squall", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Bite Bug" }]
        }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => session.AdvanceTurn());
        hud.Navigate(MenuInput.Confirm);
        using Bitmap bitmap = new(960, 672, PixelFormat.Format32bppArgb);
        using (BattlePreviewCanvas canvas = new(bitmap))
        {
            UiView view = hud.BuildView();
            view.Resize(bitmap.Width, bitmap.Height);
            view.Draw(canvas, new UiTheme());
        }
        Assert.True(hud.SelectingTarget);
        Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(20, 20).ToArgb());
        Assert.NotEqual(Color.Black.ToArgb(), bitmap.GetPixel(20, 500).ToArgb());
        Assert.Equal(0, session.Frame);
        Assert.Empty(session.QueuedActions);
    }
}
