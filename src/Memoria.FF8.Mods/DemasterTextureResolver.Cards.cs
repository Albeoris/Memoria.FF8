using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class DemasterTextureResolver
{
    private static bool IsCardArtworkTarget(string stem)
    {
        return Enumerable.Range(0, 10).Any(index => stem.Equals($"textures/mc{index:00}.tex", StringComparison.OrdinalIgnoreCase))
            || stem.Equals("textures/mc05.tex_en", StringComparison.OrdinalIgnoreCase)
            || Enumerable.Range(0, 14).Any(index => stem.Equals($"textures/cards/cards_{index}", StringComparison.OrdinalIgnoreCase))
            || stem.Equals("textures/cards/cards_7_en", StringComparison.OrdinalIgnoreCase)
            || new[] { "back_0", "back_1", "board_0", "board_1", "text_0" }.Any(name => stem.Equals("textures/cards/" + name, StringComparison.OrdinalIgnoreCase));
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
        return new(image, $"{leftFile?.PackageId}/{leftFile?.RelativePath}@{leftFile?.Revision}+{rightFile?.PackageId}/{rightFile?.RelativePath}@{rightFile?.Revision}", context.LogicalWidth, context.LogicalHeight);
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
