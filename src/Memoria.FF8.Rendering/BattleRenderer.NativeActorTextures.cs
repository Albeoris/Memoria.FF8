using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    private static readonly ConditionalWeakTable<NativeBattleEffectTextureState, Dictionary<(ushort Page, ushort Clut), PixelImage>> ActorTexturePages = new();
    private static readonly ConditionalWeakTable<PixelImage, PixelImage> PetrifiedActorTextures = new();

    private static PixelImage CreatePetrifiedActorTexture(PixelImage source)
    {
        PixelImage result = new(source.Width, source.Height);
        for (int index = 0; index < source.Pixels.Length; index++)
        {
            Rgba32 pixel = source.Pixels[index];
            int level = NativeBattleStatusPalette.PetrifyLevel(pixel.R >> 3, pixel.G >> 3, pixel.B >> 3);
            byte gray = (byte)((level << 3) | (level >> 2));
            result.Pixels[index] = new(gray, gray, gray, pixel.A);
        }
        return result;
    }

    private static PixelImage NativeActorTexturePage(NativeBattleEffectTextureState state, ushort page, ushort clut)
    {
        Dictionary<(ushort Page, ushort Clut), PixelImage> pages = ActorTexturePages.GetValue(state, _ => []);
        lock (pages)
        {
            if (!pages.TryGetValue((page, clut), out PixelImage? image))
            {
                image = NativeBattleEffectTextureReader.DecodeRegion(state, page, clut, 0, 0, 256, 256);
                pages.Add((page, clut), image);
            }
            return image;
        }
    }
}
