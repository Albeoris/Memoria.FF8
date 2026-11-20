using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class DemasterTextureResolver
{
    private static bool IsCardArtworkTarget(string stem)
    {
        return DescribeCardArtworkTarget(stem) is not null;
    }

    internal static (string Owner, IReadOnlyList<string> Members)? DescribeCardArtworkTarget(string stem)
    {
        stem = stem.ToLowerInvariant();
        if (stem == "textures/mc05.tex_en")
        {
            stem = "textures/mc05.tex";
        }
        if (stem.Length == 17 && stem.StartsWith("textures/mc0", StringComparison.Ordinal) && char.IsAsciiDigit(stem[12]) && stem.EndsWith(".tex", StringComparison.Ordinal))
        {
            return ("menu/" + stem[9..], ["atlas"]);
        }
        const string root = "textures/cards/";
        if (!stem.StartsWith(root, StringComparison.Ordinal))
        {
            return null;
        }
        string name = stem[root.Length..];
        if (name == "cards_7_en")
        {
            name = "cards_7";
        }
        if (name.StartsWith("cards_", StringComparison.Ordinal) && int.TryParse(name.AsSpan(6), out int sheet) && sheet is >= 0 and < 14 && name == $"cards_{sheet}")
        {
            List<string> members = [];
            for (int id = sheet * 8; id < Math.Min(110, (sheet + 1) * 8); id++)
            {
                members.Add($"front/{id}");
            }
            if (sheet == 13)
            {
                members.Add("back");
            }
            return ("cards/cards", members);
        }
        return name switch
        {
            "back_0" or "back_1" => ("cards/cards", ["background"]),
            "board_0" or "board_1" => ("cards/cards", ["board-background"]),
            "text_0" => ("cards/cards", Enumerable.Range(0, 8).Select(index => $"element/{index}").ToArray()),
            _ => null
        };
    }

    private TextureReplacement? ResolveCardArtwork(TextureBuildContext context)
    {
        string owner = context.Key.Owner.Path;
        string part = context.Key.Subresource;
        bool english = context.Source.Language is not ("jp" or "ja");
        if (part == "atlas" && owner.StartsWith("menu/mc", StringComparison.Ordinal) && owner.Length == 13 && int.TryParse(owner.AsSpan(7, 2), out int menuAtlas) && menuAtlas is >= 0 and <= 9 && owner.EndsWith(".tex", StringComparison.Ordinal))
        {
            return ReadCardRegion(context, "textures/" + owner[5..] + (menuAtlas == 5 && english ? "_en" : ""), 0, 0, 256, 256);
        }
        if (owner != "cards/cards")
        {
            return null;
        }
        if (part.StartsWith("front/", StringComparison.Ordinal) && int.TryParse(part.AsSpan(6), out int id) && id is >= 0 and < 110)
        {
            int atlas = id / 8;
            int cell = id % 8;
            return ReadCardRegion(context, $"textures/cards/cards_{atlas}" + (atlas == 7 && english ? "_en" : ""), cell % 2 * 64, cell / 2 * 64, 64, 64);
        }
        if (part.StartsWith("element/", StringComparison.Ordinal) && int.TryParse(part.AsSpan(8), out int element) && element is >= 0 and < 8)
        {
            return ReadCardRegion(context, "textures/cards/text_0", element % 4 * 64, (element / 4 + 1) * 16, 16, 16);
        }
        if (part == "back")
        {
            return ReadCardRegion(context, "textures/cards/cards_13", 0, 192, 64, 64);
        }
        if (part is "background" or "board-background")
        {
            return ReadCardBackground(context, part == "background" ? "back" : "board");
        }
        return null;
    }

    private ModFile? CardFile(string stem) => WithExtensions(stem).SelectMany(path => catalog.FindAll(ModFormat.Demaster, path)).FirstOrDefault();

    private TextureReplacement? ReadCardRegion(TextureBuildContext context, string stem, int x, int y, int width, int height)
    {
        ModFile? file = CardFile(stem);
        if (file is null)
        {
            return null;
        }
        PixelImage sheet = ModImageDecoder.Decode(file).Image;
        int scale = CardSheetScale(file, sheet);
        PixelImage image = new(width * scale, height * scale);
        CopyCardRegion(sheet, image, x * scale, y * scale, 0, width * scale, height * scale);
        return new(image, $"{file.PackageId}/{file.RelativePath}@{file.Revision}", context.LogicalWidth, context.LogicalHeight);
    }

    private TextureReplacement? ReadCardBackground(TextureBuildContext context, string name)
    {
        ModFile? leftFile = CardFile($"textures/cards/{name}_0");
        ModFile? rightFile = CardFile($"textures/cards/{name}_1");
        if (leftFile is null && rightFile is null)
        {
            return null;
        }
        PixelImage? left = leftFile is null ? null : ModImageDecoder.Decode(leftFile).Image;
        PixelImage? right = rightFile is null ? null : ModImageDecoder.Decode(rightFile).Image;
        int scale = left is null ? CardSheetScale(rightFile!, right!) : CardSheetScale(leftFile!, left);
        if (right is not null && CardSheetScale(rightFile!, right) != scale)
        {
            throw new ModTextureLoadException(rightFile!.PackageId, rightFile.RelativePath, new InvalidDataException("Triple Triad background halves must have the same scale."));
        }
        PixelImage image = new(384 * scale, 224 * scale);
        PixelImage original = context.Original;
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[x, y] = original[x * original.Width / image.Width, y * original.Height / image.Height];
            }
        }
        if (left is not null)
        {
            CopyCardRegion(left, image, 0, 0, 0, 256 * scale, 224 * scale);
        }
        if (right is not null)
        {
            CopyCardRegion(right, image, 0, 0, 256 * scale, 128 * scale, 224 * scale);
        }
        string provenance = string.Join(';', new[] { leftFile, rightFile }.OfType<ModFile>().Select(file => $"{file.PackageId}/{file.RelativePath}@{file.Revision}"));
        return new(image, provenance, context.LogicalWidth, context.LogicalHeight);
    }

    private static int CardSheetScale(ModFile file, PixelImage sheet)
    {
        if (sheet.Width < 256 || sheet.Width % 256 != 0 || sheet.Height != sheet.Width)
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("Triple Triad sheets must preserve the 256 by 256 canvas at an integer scale."));
        }
        return sheet.Width / 256;
    }

    private static void CopyCardRegion(PixelImage source, PixelImage target, int x, int y, int targetX, int width, int height)
    {
        for (int row = 0; row < height; row++)
        {
            source.Pixels.AsSpan((y + row) * source.Width + x, width).CopyTo(target.Pixels.AsSpan(row * target.Width + targetX, width));
        }
    }
}
