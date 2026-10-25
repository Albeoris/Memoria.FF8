using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class DemasterTextureResolver
{
    private const string BattlePageLayout = "native-battle-stage-page";
    private const string BattlePagePrefix = "textures/battle.fs/hd_new/a0stg";

    private TextureReplacement? ResolveBattlePage(TextureBuildContext context)
    {
        if (!TryBattlePageStem(context, out string stem))
        {
            return null;
        }
        ModFile? first = FindBattlePageFile($"{stem}_0");
        if (first is null)
        {
            ModFile? file = FindBattlePageFile(stem);
            if (file is null)
            {
                return null;
            }
            DecodedModImage image = DecodeBattlePage(file);
            return new(image.Image, BattlePageProvenance(file), 256, 256, image.MipLevels);
        }
        List<PixelImage> frames = [];
        List<string> provenance = [];
        DecodedModImage firstImage = DecodeBattlePage(first);
        frames.Add(firstImage.Image);
        provenance.Add(BattlePageProvenance(first));
        // A missing numbered frame restarts at zero; later numbers are unreachable.
        for (int index = 1; ; index++)
        {
            ModFile? file = FindBattlePageFile($"{stem}_{index}");
            if (file is null)
            {
                break;
            }
            DecodedModImage image = DecodeBattlePage(file);
            if (image.Image.Width != firstImage.Image.Width || image.Image.Height != firstImage.Image.Height)
            {
                throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("Demaster battle page animation frames must have identical dimensions."));
            }
            frames.Add(image.Image);
            provenance.Add(BattlePageProvenance(file));
        }
        return new TextureReplacement(firstImage.Image, string.Join(";", provenance), 256, 256, firstImage.MipLevels)
        {
            Animation = new TextureAnimation(frames.AsReadOnly(), 100)
        };
    }

    private ModFile? FindBattlePageFile(string stem)
    {
        return WithExtensions(stem).SelectMany(path => catalog.FindAll(ModFormat.Demaster, path)).FirstOrDefault();
    }

    private static string BattlePageProvenance(ModFile file) => $"{file.PackageId}/{file.RelativePath}@{file.Revision}";

    private static DecodedModImage DecodeBattlePage(ModFile file)
    {
        DecodedModImage decoded = ModImageDecoder.Decode(file);
        if (decoded.Image.Width <= 0 || decoded.Image.Height <= 0)
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("Demaster battle pages require positive image dimensions."));
        }
        return decoded;
    }

    private static bool TryBattlePageStem(TextureBuildContext context, out string stem)
    {
        stem = "";
        string owner = context.Key.Owner.Path;
        string page = context.Key.Subresource;
        if (!HasLayout(context.Source, BattlePageLayout) || context.LogicalWidth != 256 || context.LogicalHeight != 256 || context.Key.Palette < 0 || owner.Length != 17 || !owner.StartsWith("battle/a0stg", StringComparison.Ordinal) || !owner.EndsWith(".x", StringComparison.Ordinal) || !TryNonnegative(owner[12..15], out int stage) || !owner.Equals($"battle/a0stg{stage:D3}.x", StringComparison.Ordinal) || !page.StartsWith("page/", StringComparison.Ordinal) || !TryNonnegative(page[5..], out int nativePage) || nativePage is < 16 or > 21 || page != $"page/{nativePage}")
        {
            return false;
        }
        stem = $"{BattlePagePrefix}{stage:D3}_{nativePage}";
        return true;
    }

    private static bool IsBattlePageTarget(string stem)
    {
        if (!stem.StartsWith(BattlePagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        string[] parts = stem[BattlePagePrefix.Length..].Split('_');
        return parts.Length is 2 or 3 && TryNonnegative(parts[0], out int stage) && stage <= 999 && parts[0] == $"{stage:D3}" && TryNonnegative(parts[1], out int page) && page is >= 16 and <= 21 && parts[1] == $"{page}" && (parts.Length == 2 || TryNonnegative(parts[2], out int frame) && parts[2] == $"{frame}");
    }
}
