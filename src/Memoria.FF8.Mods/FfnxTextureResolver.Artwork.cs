using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class FfnxTextureResolver
{
    private TextureReplacement? ResolveArtwork(TextureBuildContext context)
    {
        string owner = context.Key.Owner.Path;
        string part = context.Key.Subresource;
        if (owner.StartsWith("menu/mc", StringComparison.Ordinal) && owner.EndsWith(".tex", StringComparison.Ordinal) && owner.Length == 13 && Number(owner[7..9], out int menu) && menu < 10 && part == "atlas" && context.Source.Language is null or "en")
        {
            return ReadRegion(context, $"data/eng/menu/mc{menu:00}", 0, 0, 0, 256, 256, 256, 256);
        }
        if (owner != "cards/cards")
        {
            return null;
        }
        if (part.StartsWith("front/", StringComparison.Ordinal) && Number(part[6..], out int card) && card < 110)
        {
            return ReadRegion(context, "cardgame/cards", 0, card / 8 * 128 + card % 2 * 64, card % 8 / 2 * 64, 64, 64, 1792, 256);
        }
        if (part == "back")
        {
            return ReadRegion(context, "cardgame/cards", 0, 1664, 192, 64, 64, 1792, 256);
        }
        if (part.StartsWith("element/", StringComparison.Ordinal) && Number(part[8..], out int element) && element < 8)
        {
            return ReadRegion(context, "cardgame/icons", 0, element % 4 * 64, (element / 4 + 1) * 16, 16, 16, 768, 192);
        }
        if (part is "background" or "board-background")
        {
            return ReadRegion(context, part == "background" ? "cardgame/intro" : "cardgame/game", 0, 0, 0, 384, 224, 384, 224);
        }
        return null;
    }
}
