using System.Drawing;
using System.Drawing.Imaging;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattlePreviewCanvasTests
{
    [Theory]
    [InlineData(UiImageKind.CharacterPortrait, 4)]
    [InlineData(UiImageKind.GuardianForcePortrait, 0)]
    [InlineData(UiImageKind.Card, 109)]
    [InlineData(UiImageKind.Icon, 192)]
    public void Original_foreground_artwork_is_visible_and_respects_nested_clipping(UiImageKind kind, int id)
    {
        using Bitmap bitmap = new(180, 160, PixelFormat.Format32bppArgb);
        using (BattlePreviewCanvas canvas = new(bitmap))
        {
            canvas.PushClip(new UiRect(30, 20, 80, 100));
            canvas.Image(new UiImageSource(kind, id), new UiRect(10, 10, 120, 120));
            canvas.PopClip();
        }
        int colored = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color color = bitmap.GetPixel(x, y);
                if (color.R != 0 || color.G != 0 || color.B != 0)
                {
                    Assert.InRange(x, 30, 109);
                    Assert.InRange(y, 20, 119);
                    colored++;
                }
            }
        }
        Assert.True(colored > 20, $"Original {kind} {id} was not visible.");
    }

    [Theory]
    [InlineData(14, 240)]
    [InlineData(24, 240)]
    [InlineData(18, 90)]
    public void Centered_text_keeps_actual_glyphs_in_the_middle_of_the_row(float size, int width)
    {
        using Bitmap bitmap = new(300, 120, PixelFormat.Format32bppArgb);
        using (BattlePreviewCanvas canvas = new(bitmap))
        {
            canvas.Text("Refresh packages", size, new UiRect(10, 10, width, 100), new UiColor(255, 255, 255), UiAlignment.Center);
        }
        int top = bitmap.Height;
        int bottom = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).R > 200)
                {
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }
        }
        Assert.True(bottom >= top);
        Assert.InRange((top + bottom + 1) / 2f, 59, 61);
    }

    [Fact]
    public void Rich_battle_message_centers_each_line_and_preserves_native_colors()
    {
        using Bitmap bitmap = new(400, 160, PixelFormat.Format32bppArgb);
        using (BattlePreviewCanvas canvas = new(bitmap))
        {
            DialogueDocument message = DialogueTextFormatter.Parse("{Color Red}Ready", new Dictionary<int, string>());
            canvas.BattleMessage(message, 20, new UiRect(50, 20, 300, 100), 0);
        }
        int left = bitmap.Width;
        int right = -1;
        for (int y = 20; y < 120; y++)
        {
            for (int x = 50; x < 350; x++)
            {
                Color color = bitmap.GetPixel(x, y);
                if (color.R > 150 && color.G < 70 && color.B < 70)
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                }
            }
        }
        Assert.True(right > left);
        Assert.InRange((left + right) / 2f, 196, 204);
    }

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
        Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(300, 550).ToArgb());
        Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(940, 620).ToArgb());
        Assert.NotEqual(Color.Black.ToArgb(), bitmap.GetPixel(32, 520).ToArgb());
        Assert.Equal(0, session.Frame);
        Assert.Empty(session.QueuedActions);
    }
}
