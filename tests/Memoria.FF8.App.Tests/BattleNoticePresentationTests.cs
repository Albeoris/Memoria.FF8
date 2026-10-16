using System.Drawing;
using System.Drawing.Imaging;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleNoticePresentationTests
{
    [Theory]
    [InlineData(960, 672)]
    [InlineData(1920, 1344)]
    public void System_notice_is_half_width_and_centers_text_at_both_scales(int width, int height)
    {
        using Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
        using (BattlePreviewCanvas canvas = new(bitmap))
        {
            UiView view = new(new NoticeElement("Quick saved."));
            view.Resize(width, height);
            view.Draw(canvas, new UiTheme());
        }
        int scale = width / 960;
        Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(200 * scale, 40 * scale).ToArgb());
        Assert.NotEqual(Color.Black.ToArgb(), bitmap.GetPixel(241 * scale, 40 * scale).ToArgb());
        Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(740 * scale, 40 * scale).ToArgb());
        int left = width;
        int right = 0;
        for (int y = 30 * scale; y < 80 * scale; y++)
        {
            for (int x = 260 * scale; x < 700 * scale; x++)
            {
                Color color = bitmap.GetPixel(x, y);
                if (color.R > 200 && color.G > 200 && color.B > 200)
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                }
            }
        }
        Assert.True(right > left);
        Assert.InRange((left + right) / 2f, width / 2f - 8 * scale, width / 2f + 8 * scale);
    }
}
