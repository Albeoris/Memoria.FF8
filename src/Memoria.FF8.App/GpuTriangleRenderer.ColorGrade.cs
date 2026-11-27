using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

public sealed partial class GpuTriangleRenderer
{
    private const int FirstColorGradeSlot = 9;
    private const int SecondColorGradeSlot = 10;
    private ColorLookupTable? firstColorLookup;
    private ColorLookupTable? secondColorLookup;
    private Texture2D? firstColorTexture;
    private Texture2D? secondColorTexture;

    private void ApplyColorGradeState(SceneColorGrade grade)
    {
        effect.Parameters["SceneLinearTint"].SetValue(grade.LinearTint is { } tint ? new Vector4(tint.X, tint.Y, tint.Z, 1) : Vector4.Zero);
        effect.Parameters["ColorGradeParameters"].SetValue(grade.Enabled
            ? new Vector4(grade.First?.Size ?? 0, grade.Second?.Size ?? 0, grade.Mix, grade.Strength)
            : Vector4.Zero);
        if (!grade.Enabled)
        {
            return;
        }
        UpdateLookup(ref firstColorLookup, ref firstColorTexture, grade.First, "ColorGradeFirst", FirstColorGradeSlot);
        UpdateLookup(ref secondColorLookup, ref secondColorTexture, grade.Second, "ColorGradeSecond", SecondColorGradeSlot);
    }

    private void UpdateLookup(ref ColorLookupTable? previous, ref Texture2D? texture, ColorLookupTable? lookup, string parameter, int slot)
    {
        if (ReferenceEquals(previous, lookup))
        {
            return;
        }
        Texture2D? next = null;
        if (lookup is not null)
        {
            PixelImage strip = lookup.Strip;
            next = new Texture2D(device, strip.Width, strip.Height, false, SurfaceFormat.Color);
            try
            {
                next.SetData(strip.Pixels);
            }
            catch
            {
                next.Dispose();
                throw;
            }
        }
        device.Textures[slot] = null;
        effect.Parameters[parameter].SetValue(next);
        texture?.Dispose();
        texture = next;
        previous = lookup;
    }

    private void DisposeColorGrade()
    {
        firstColorTexture?.Dispose();
        secondColorTexture?.Dispose();
    }
}
