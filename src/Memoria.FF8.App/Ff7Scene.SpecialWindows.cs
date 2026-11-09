using System.Globalization;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private bool DrawSpecialWindow(SpriteBatch batch, Ff7Dialogue dialogue, Ff7Window window, Rectangle bounds)
    {
        if (window.SpecialType == 0)
        {
            return false;
        }
        if (window.SpecialType != 1)
        {
            throw new NotSupportedException($"FFVII window special display {window.SpecialType} requires its native value binding.");
        }
        UiPreferences preferences = PresentationPreferences;
        float scale = bounds.Width / 320f;
        float size = 21 * scale * preferences.TextScale / 100f;
        Vector2 origin = new(bounds.X + (window.X + window.SpecialX) * scale, bounds.Y + (window.Y + window.SpecialY) * scale);
        string value = Ff7CountdownText.Format(Session!.Clock.CountdownSeconds);
        float[] offsets = [0, 16, 32, 40, 56];
        for (int index = 0; index < value.Length; index++)
        {
            dialogueText!.DrawLine(batch, value[index].ToString(CultureInfo.InvariantCulture), size, origin + new Vector2(offsets[index] * scale, 0), Color.White, preferences.UiFontFamily);
        }
        if (Session.Host.Process.FadeState?.QuadColorArgb is uint quad && (quad >> 24) != 0)
        {
            batch.Draw(pixel, origin, null, new Color(0, 0, 0, (int)(quad >> 24)), 0, Vector2.Zero, new Vector2(72, 21) * scale, SpriteEffects.None, 0);
        }
        return true;
    }
}

internal static class Ff7CountdownText
{
    internal static string Format(uint seconds)
    {
        seconds = Math.Min(seconds, 5999);
        return (seconds / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
    }
}
