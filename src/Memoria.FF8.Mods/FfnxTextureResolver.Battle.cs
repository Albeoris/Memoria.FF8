using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class FfnxTextureResolver
{
    private TextureReplacement? ResolveBattle(TextureBuildContext context)
    {
        TextureSourceAddress? address = context.NativeAlias;
        string owner = address?.Owner.Path ?? context.Key.Owner.Path;
        string[] parts = (address?.Subresource ?? context.Key.Subresource).Split('/');
        if (!IsBattleOwner(owner) || parts.Length != 2 || parts[0] != "texture" || !Number(parts[1], out int member))
        {
            return null;
        }
        string name = $"{owner}-{member}";
        int palette = address?.Palette ?? context.Key.Palette;
        int horizontalPageWidth = context.Region.BitsPerPixel is 4 or 8 ? 64 * (16 / context.Region.BitsPerPixel) : 0;
        int verticalPageHeight = horizontalPageWidth > 0 ? owner.EndsWith(".x", StringComparison.Ordinal) ? 256 : 128 : 0;
        TextureReplacement? result = ReadPalette(palette);
        if (result is null)
        {
            return null;
        }
        string[] roots = context.Source.Language is null or "en" ? ["mods/textures/eng/", "mods/textures/"] : ["mods/textures/"];
        Dictionary<string, Albeoris.Games.FF8.Imaging.PixelImage> variants = [];
        foreach (int available in catalog.Files.Where(file => file.Format == ModFormat.Junction).Select(file => ExternalPalette(file.RelativePath, roots, name)).Where(value => value >= 0).Distinct().Order())
        {
            variants.Add($"palette/{available}", ReadPalette(available)!.Image);
        }
        return result with { Variants = variants };

        TextureReplacement? ReadPalette(int value) => address is null
            ? ReadRegion(context, name, value, 0, 0, context.LogicalWidth, context.LogicalHeight, context.LogicalWidth, context.LogicalHeight, horizontalPageWidth, verticalPageHeight, normalizedMaterial: true)
            : ReadRegion(context, name, value, address.X, address.Y, address.Width, address.Height, address.CanvasWidth, address.CanvasHeight, horizontalPageWidth, verticalPageHeight, normalizedMaterial: true);
    }

    private static bool IsBattleOwner(string name) => System.Text.RegularExpressions.Regex.IsMatch(name, @"^battle/(a0stg[0-9]{3}\.x|c0m[0-9]{3}\.dat|d[0-9a-f][cw][0-9]{3}\.dat)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static bool IsAbsentNativeBattleMember(IResourceSource source, string target, Dictionary<ResourceKey, int?> counts)
    {
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(target, @"^(?:eng/)?(battle/[^/]+)-([0-9]+)_([0-9]+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (!match.Success || !Number(match.Groups[2].Value, out int member) || !PaletteNumber(match.Groups[3].Value, out _))
        {
            return false;
        }
        ResourceKey owner = new(match.Groups[1].Value);
        if (!counts.TryGetValue(owner, out int? count))
        {
            ResourceContent? content = source.Find(owner);
            count = content is null ? 0 : ReadBattleTextureCount(owner, content);
            counts.Add(owner, count);
        }
        return count is int present && member >= present;
    }

    private static int? ReadBattleTextureCount(ResourceKey owner, ResourceContent content)
    {
        if (!IsBattleOwner(owner.Path))
        {
            return null;
        }
        if (owner.Path.EndsWith(".x", StringComparison.Ordinal))
        {
            return 1;
        }
        using Stream input = content.OpenRead();
        using MemoryStream? materialized = input.CanSeek ? null : new MemoryStream();
        if (materialized is not null)
        {
            input.CopyTo(materialized);
            materialized.Position = 0;
        }
        Stream stream = materialized ?? input;
        using BinaryReader reader = new(stream, System.Text.Encoding.UTF8, true);
        int sections = reader.ReadInt32();
        int section = sections switch
        {
            11 => 10,
            9 or 10 => 8,
            7 or 8 => owner.Path[9] == 'w' ? 6 : 5,
            5 => 4,
            _ => -1
        };
        if (section < 0)
        {
            return null;
        }
        stream.Position = 4 + section * 4;
        int offset = reader.ReadInt32();
        if (offset < 8 + sections * 4 || offset > stream.Length - 4)
        {
            throw new InvalidDataException("Original battle texture directory leaves its model resource.");
        }
        stream.Position = offset;
        int count = reader.ReadInt32();
        if (count < 0 || count > 256 || stream.Length - offset < 4L + count * 4L)
        {
            throw new InvalidDataException("Original battle texture member count leaves its resource.");
        }
        return count;
    }

    private static bool IsSupportedBattleTarget(string name)
    {
        int separator = name.LastIndexOf('_');
        int memberSeparator = name.LastIndexOf('-');
        return separator > memberSeparator && memberSeparator > 0
            && PaletteNumber(name[(separator + 1)..], out _) && Number(name[(memberSeparator + 1)..separator], out _)
            && IsBattleOwner(name[..memberSeparator]);
    }

    private static bool IsNeverGeneratedBattlePaletteTarget(string name)
    {
        if (name.StartsWith("eng/", StringComparison.OrdinalIgnoreCase))
        {
            name = name[4..];
        }
        int separator = name.LastIndexOf('_');
        int memberSeparator = name.LastIndexOf('-');
        return separator > memberSeparator && memberSeparator > 0
            && IsBattleOwner(name[..memberSeparator]) && Number(name[(memberSeparator + 1)..separator], out _)
            && !PaletteNumber(name[(separator + 1)..], out _);
    }
}
