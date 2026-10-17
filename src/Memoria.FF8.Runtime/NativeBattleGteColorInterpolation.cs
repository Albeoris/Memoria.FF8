using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>45E120/45F4C0/45E370 polygon color FIFO followed by the separate first-color45F270 operation.</summary>
public static class NativeBattleGteColorInterpolation
{
    public static ImmutableArray<uint> Gouraud45F4C0(ImmutableArray<uint> colors, bool quad, int factor, uint farColor, NativeBattleSummonColors rules)
    {
        if (colors.IsDefault || colors.Length != (quad ? 4 : 3))
        {
            throw new InvalidDataException("Native polygon depth cue requires its actual three or four color DWORDs.");
        }
        // 45E120 sets RGBC from its third input: triangle's modified first color, quad's original fourth color.
        uint fifoCommand = quad ? colors[3] & 0xFF000000 : colors[0] & 0xFF000000;
        ImmutableArray<uint>.Builder result = colors.ToBuilder();
        for (int corner = 1; corner < colors.Length; corner++)
        {
            result[corner] = (rules.Interpolate(colors[corner], factor, farColor) & 0xFFFFFF) | fifoCommand;
        }
        result[0] = rules.Interpolate(colors[0], factor, farColor);
        return result.ToImmutable();
    }
}
