using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Remastered;

/// <summary>Original native GF atlas banks selected by observed source-copy coordinates.</summary>
public static class RemasteredBattleGfAtlasBanks
{
    public static TextureSourceAtlasAddress Apply(TextureSourceAtlasAddress address, int identity, int component, NativeBattleTextureRegistration registration, TexturePrimitiveSample? sample = null)
    {
        int canvasWidth = identity switch
        {
            95 => 512,
            184 => 384,
            99 when component == 1 => 384,
            _ => 256
        };
        if (identity is not (95 or 184 or 902) && (identity != 99 || component != 1))
        {
            return address;
        }
        address = address with { CanvasWidth = canvasWidth };
        if (registration.CopyOrder is not { } order || order.IsDefault)
        {
            return address with { UnsupportedReason = "Native GF atlas selection requires retained source-copy observation order. Migrate the selected battle state before using this atlas." };
        }
        int bank = 0;
        for (int index = order.Length - 1; index >= 0; index--)
        {
            int frame = order[index];
            if ((uint)frame >= registration.CopyFrames.Length)
            {
                throw new InvalidDataException("Native GF atlas copy order leaves its retained source rectangles.");
            }
            NativeBattleEffectTextureRectangle rectangle = registration.CopyFrames[frame];
            if (Bank(identity, rectangle.X, rectangle.Y) is { } selectedBank)
            {
                bank = selectedBank;
                break;
            }
        }
        if (bank == 0)
        {
            return address;
        }
        if (sample is not { } selectedSample)
        {
            return address with { UnsupportedReason = "Native GF atlas bank selection requires the primitive's original normalized first texture coordinates." };
        }
        if (!float.IsFinite(selectedSample.FirstU) || !float.IsFinite(selectedSample.FirstV))
        {
            throw new InvalidDataException("Native GF atlas bank selection requires finite original texture coordinates.");
        }
        bool upperHalf = selectedSample.FirstV < 0.5f;
        bool selectBank = identity == 95 ? !upperHalf : upperHalf;
        return address with { SampleXOffset = selectBank ? bank * 128 : 0 };
    }

    private static int? Bank(int identity, int x, int y)
    {
        return (identity, x, y) switch
        {
            (184, 854, 256) => 0,
            (184, 864, 289) => 1,
            (184, 862, 256) => 2,
            (902, 865, 279) => 0,
            (902, 891, 302) => 1,
            (99, 704, 315) => 0,
            (99, 704, 328) => 1,
            (99, 711, 322) => 2,
            (95, 891, 403) => 3,
            (95, 886, 403) => 0,
            (95, 863, 386) => 0,
            (95, 870, 386) => 1,
            (95, 877, 386) => 2,
            _ => null
        };
    }
}
